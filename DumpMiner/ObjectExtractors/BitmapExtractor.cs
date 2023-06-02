using DumpMiner.Debugger;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DumpMiner.ObjectExtractors
{
    class BitmapExtractor : IObjectExtractor
    {
        public string GetFileNameSuffix()
        {
            return "_bitmap.bmp";
        }

        private ulong? ReadPointer(ulong address)
        {
            byte[] buffer = new byte[DebuggerSession.Instance.DataTarget.PointerSize];

            if (!DebuggerSession.Instance.DataTarget.ReadProcessMemory(address, buffer, buffer.Length, out var br))
            {
                return null;
            }

            if (br != buffer.Length)
            {
                return null;
            }

            ulong addr = buffer.Length == 4
                ? BitConverter.ToUInt32(buffer, 0)
                : BitConverter.ToUInt64(buffer, 0);

            return addr;
        }

        private uint? ReadDWORD(ulong address)
        {
            byte[] buffer = new byte[4];

            if (!DebuggerSession.Instance.DataTarget.ReadProcessMemory(address, buffer, buffer.Length, out var br))
            {
                return null;
            }

            if (br != buffer.Length)
            {
                return null;
            }

            return BitConverter.ToUInt32(buffer, 0);
        }

        public Task<bool> Extract(string path, ulong address, ulong size, string typeName)
        {
            try
            {
                // only support 32-bit images for now
                if (DebuggerSession.Instance.DataTarget.PointerSize != 4)
                {
                    App.Dialog.ShowDialog("Only 32-bit targets are currently supported for bitmap extraction.", title: "Error");
                    return Task.FromResult(false);
                }

                // get the type at the target address, so we can read the fields
                var heap = DebuggerSession.Instance.Runtime.Heap;
                ClrType type = heap.GetObjectType(address);
                if (type == null)
                {
                    return Task.FromResult(false);
                }

                // get the nativeImage field. this points to a native gdiplus!GpBitmap object.
                var nativeBitmapField = type.GetFieldByName("nativeImage");
                if (nativeBitmapField == null)
                {
                    return Task.FromResult(false);
                }

                /*
                 * We're going to try to get to a gdiplus!GpMemoryBitmap object from here.
                 * The overall path is: GpBitmap -> CopyOnWriteBitmap -> GpMemoryBitmap -> Raw pixel data
                 * 
                 * Values given here are for 32-bit. I need to figure this out for 64-bit yet.
                 * 
                 * These structures are undocumented and their fields are no longer included in PDBs for gdiplus. I'm having to go from old struct dumps from a random MSDN post:
                 * https://social.msdn.microsoft.com/Forums/en-US/cd86ddf7-da4b-4b85-a737-b516dbb13d03/windows-form-application-use-a-huge-amount-of-memory-in-vista-but-not-in-xp?forum=winforms
                 * 
                 * GpBitmap looks like this:
                 *  +0x00 void* VTablePtr;
                 *  +0x04 DWORD Tag; // should equal 0x676d4931, i.e. ObjectTagImage
                 *  +0x08 DWORD Uid; // usually 0
                 *  +0x0c DWORD ImgType; // should equal 1, i.e. ImageTypeBitmap
                 *  +0x10 DWORD Lockable; // usually 0xffffffff
                 *  +0x14 CopyOnWriteBitmap* InternalBitmap; // <------ THIS IS THE POINTER WE NEED
                 *  +0x16 DWORD ScanBitmapRef; // usually 1, no idea what this does
                 *  +0x16 EpScanBitmap ScanBitmap; // not a pointer, this struct follows immediately on (it also starts with a vtable pointer)
                 * 
                 * CopyOnWriteBitmap looks like this:
                 *  +0x00 void* VTablePtr;
                 *  +0x04 DWORD RefCount; // should never be zero.
                 *  +0x08 _RTL_CRITICAL_SECTION Semaphore; // usually starts with two 0xffffffff DWORDs.
                 *  +0x20 DWORD State; // usually 4
                 *  +0x24 DWORD ObjRefCount; // should never be zero
                 *  +0x28 WCHAR* FileName; // contains the path to the file
                 *  +0x2c void* FileStream; // points to a filestream object if you opened the image from a stream and the stream stil exists
                 *  +0x30 void* Img; // not entire sure what this is? appears to be populated if you open a jpeg/png/whatever so probably used for decoding
                 *  +0x34 GdMemoryBitmap* Bmp; // <------ THIS IS THE POINTER WE NEED
                 *  +0x38 DWORD CurrentFrameIndex; // animated gifs
                 *  +0x3c void* CleanupBitmapData; // no idea
                 *  +0x40 void* EncoderPtr; // related to png/jpeg/whatever encoding
                 *  +0x44 DWORD SpecialJPEGSave; // some flag, probably a BOOL. usually 0
                 *  +0x48 DWORD ICMConvert; // probably a BOOL, usually 0, set 1 if ICM profile is to be saved when the image is saved
                 *  +0x4c DWORD Display; // might be a BOOL? set to 1 in every case I've seen
                 *  +0x50 DWORD XDpiOverride; // probably a BOOL that tells it to not do DPI scaling horizontally
                 *  +0x54 DWORD YDpiOverride; // same again, vertically
                 *  +0x58 DWORD DirtyFlag; // probably a BOOL, probably set whenever a change has been made after load
                 *  ... a bunch more random stuff
                 *  +0xa0 DWORD PixelFormatInMem;   // useful because this _may_ differ from what the bitmap's pixel format is, but I think this gets repeated in GpMemoryBitmap anyway
                 *                                  // these are gdiplus pixel format values. see https://github.com/VFPX/CodePlex/blob/master/VFP9/Ffc/gdiplus.h#L377 and https://vfpimaging.blogspot.com/2006/03/drawing-on-gifs-or-indexed-pixel-format_3176.html
                 *  ... more fields of little use
                 *  
                 *  GpMemoryBitmap looks like this:
                 *   +0x00 void* VTablePtr0; // four pointers into gdiplus!GpMemoryBitmap::`vftable'.
                 *   +0x04 void* VTablePtr1; // these aren't all equal but they should be within 0x200 of each other in total
                 *   +0x08 void* VTablePtr2;
                 *   +0x0c void* VTablePtr3;
                 *   +0x10 DWORD Width; // width of the image in pixels
                 *   +0x14 DWORD Height; // height of the image in pixels
                 *   +0x18 DWORD Stride; // number of bytes that each line takes up in memory. this is usually Width multiplied by how many bytes per pixel are used, *BUT* there may be padding at the end of each row!
                 *   +0x1c DWORD PixelFormat; // pixel format for the data. should be the canonical pixel format to use here. see links above for values (0x0026200A for 32bpp ARGB is common)
                 *   +0x20 void* Scan0; // <----- THIS IS THE POINTER WE NEED. it points to the pixel buffer
                 *   +0x24 DWORD Reserved; // seems to always be set to 0x20000
                 *   +0x28 DWORD ComRefCount; // should be at least 1 
                 *   +0x2c DWORD ObjectLock; // usually 0xffffffff
                 *   ... fields after this don't quite match ref I could find, so I think this has been modified a bunch
                 * 
                 */

                // read Bitmap->nativeBitmap
                object nativeBitmapValue = nativeBitmapField.GetValue(address);
                long nativeBitmapAddrLong = (long)nativeBitmapValue;
                ulong nativeBitmapAddr = Convert.ToUInt64(nativeBitmapAddrLong);
                if (nativeBitmapAddr == 0)
                {
                    return Task.FromResult(false);
                }

                // read GpBitmap->Tag and validate
                const UInt32 ObjectTagImage = 0x676d4931;
                var gpBitmap_Tag = ReadDWORD(nativeBitmapAddr + 0x04ul);
                if ((gpBitmap_Tag ?? 0) != ObjectTagImage)
                {
                    return Task.FromResult(false);
                }

                // read GpBitmap->InternalBitmap
                var copyOnWriteBitmapAddr = ReadPointer(nativeBitmapAddr + 0x14ul);
                if ((copyOnWriteBitmapAddr ?? 0) == 0)
                {
                    return Task.FromResult(false);
                }

                // read CopyOnWriteBitmap->RefCount and validate
                var copyOnWriteBitmap_RefCount = ReadDWORD(copyOnWriteBitmapAddr.Value + 0x04ul);
                if ((copyOnWriteBitmap_RefCount ?? 0) == 0)
                {
                    return Task.FromResult(false);
                }

                // read CopyOnWriteBitmap->Bmp
                var gpMemoryBitmapAddr = ReadPointer(copyOnWriteBitmapAddr.Value + 0x34ul);
                if ((gpMemoryBitmapAddr ?? 0) == 0)
                {
                    // if this is null, it might be a decoded image that was loaded from disk
                    // unfortunately I haven't figured out how to get image data from this struct, but I can at least report the filename if it's there

                    // read CopyOnWriteBitmap->Img
                    var copyOnWriteBitmap_ImgAddr = ReadPointer(copyOnWriteBitmapAddr.Value + 0x30ul);
                    if ((copyOnWriteBitmap_ImgAddr ?? 0) == 0)
                    {
                        // no idea what's happening here, just fail
                        return Task.FromResult(false);
                    }

                    // ok, looks like we got an image that was loaded via an encoder. try to grab the path
                    var copyOnWriteBitmap_FilePathAddr = ReadPointer(copyOnWriteBitmapAddr.Value + 0x28);
                    if ((copyOnWriteBitmap_FilePathAddr ?? 0) == 0)
                    {
                        return Task.FromResult(false);
                    }

                    // we got a file path. read it.
                    byte[] filePathBuffer = new byte[1024];
                    if (!DebuggerSession.Instance.DataTarget.ReadProcessMemory(copyOnWriteBitmap_FilePathAddr.Value,
                            filePathBuffer, 1024, out var br))
                    {
                        return Task.FromResult(false);
                    }

                    if (br < 4)
                    {
                        return Task.FromResult(false);
                    }

                    string bitmapPath = Encoding.Unicode.GetString(filePathBuffer, 0, br);
                    App.Dialog.ShowDialog("This bitmap was not generated by the program, but was instead loaded from a file. The in-memory structures for this are not well understood, however a file path was recovered:\n\n" + bitmapPath, "Image file path");
                    return Task.FromResult(false);
                }

                // read GpMemoryBitmap->VTablePtr0 to GpMemoryBitmap->VTablePtr3, and validate
                var gpMemoryBitmap_VTablePtr0 = ReadPointer(gpMemoryBitmapAddr.Value);
                var gpMemoryBitmap_VTablePtr1 = ReadPointer(gpMemoryBitmapAddr.Value + 0x04ul);
                var gpMemoryBitmap_VTablePtr2 = ReadPointer(gpMemoryBitmapAddr.Value + 0x08ul);
                var gpMemoryBitmap_VTablePtr3 = ReadPointer(gpMemoryBitmapAddr.Value + 0x0cul);
                if ((gpMemoryBitmap_VTablePtr0 ?? 0) == 0 ||
                    (gpMemoryBitmap_VTablePtr1 ?? 0) == 0 ||
                    (gpMemoryBitmap_VTablePtr2 ?? 0) == 0 ||
                    (gpMemoryBitmap_VTablePtr3 ?? 0) == 0)
                {
                    return Task.FromResult(false);
                }

                ulong maxAddr = gpMemoryBitmap_VTablePtr0.Value;
                ulong minAddr = gpMemoryBitmap_VTablePtr0.Value;
                maxAddr = Math.Max(maxAddr, gpMemoryBitmap_VTablePtr1.Value);
                maxAddr = Math.Max(maxAddr, gpMemoryBitmap_VTablePtr2.Value);
                maxAddr = Math.Max(maxAddr, gpMemoryBitmap_VTablePtr3.Value);
                minAddr = Math.Min(maxAddr, gpMemoryBitmap_VTablePtr1.Value);
                minAddr = Math.Min(maxAddr, gpMemoryBitmap_VTablePtr2.Value);
                minAddr = Math.Min(maxAddr, gpMemoryBitmap_VTablePtr3.Value);
                if (maxAddr - minAddr > 0x400) // should be within 0x200 of each other, but let's expand that to 0x400 for safety
                {
                    return Task.FromResult(false);
                }

                // read width, height, stride, pixel format
                var gpMemoryBitmap_Width = ReadDWORD(gpMemoryBitmapAddr.Value + 0x10ul);
                if ((gpMemoryBitmap_Width ?? 0) == 0 || (gpMemoryBitmap_Width ?? 0) > 65535)
                {
                    return Task.FromResult(false);
                }

                var gpMemoryBitmap_Height = ReadDWORD(gpMemoryBitmapAddr.Value + 0x14ul);
                if ((gpMemoryBitmap_Height ?? 0) == 0 || (gpMemoryBitmap_Height ?? 0) > 65535)
                {
                    return Task.FromResult(false);
                }

                var gpMemoryBitmap_Stride = ReadDWORD(gpMemoryBitmapAddr.Value + 0x18ul);
                if ((gpMemoryBitmap_Stride ?? 0) == 0 || (gpMemoryBitmap_Height ?? 0) > (65535 * 8))
                {
                    return Task.FromResult(false);
                }

                var gpMemoryBitmap_PixelFormat = ReadDWORD(gpMemoryBitmapAddr.Value + 0x1cul);
                if ((gpMemoryBitmap_PixelFormat ?? 0) == 0)
                {
                    return Task.FromResult(false);
                }

                // read GpMemoryBitmap->Scan0
                var pixelDataAddr = ReadPointer(gpMemoryBitmapAddr.Value + 0x20ul);
                if ((pixelDataAddr ?? 0) == 0)
                {
                    return Task.FromResult(false);
                }

                // turn the data into a bitmap :)
                using (var bmp = new Bitmap((int)gpMemoryBitmap_Width, (int)gpMemoryBitmap_Height))
                {
                    // the PixelFormat enum uses GDI values so we can just cast directly!
                    var pixelFormat = (PixelFormat)gpMemoryBitmap_PixelFormat;
                    var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, pixelFormat);

                    try
                    {
                        // apply the stride
                        bd.Stride = (int)gpMemoryBitmap_Stride.Value;

                        // buffer for storing each line's data
                        byte[] buffer = new byte[gpMemoryBitmap_Stride.Value];
                        for (uint y = 0; y < gpMemoryBitmap_Height.Value; y++)
                        {
                            ulong lineAddr = pixelDataAddr.Value + (y * gpMemoryBitmap_Stride.Value);
                            DebuggerSession.Instance.DataTarget.ReadProcessMemory(lineAddr, buffer, (int)gpMemoryBitmap_Stride.Value, out _);
                            Marshal.Copy(buffer, 0, bd.Scan0 + (int)(y * bd.Stride), (int)gpMemoryBitmap_Stride.Value);
                        }

                    }
                    finally
                    {
                        bmp.UnlockBits(bd);
                    }

                    bmp.Save(path, ImageFormat.Bmp);
                }

                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                App.Dialog.ShowDialog(e.ToString(), title: "Error");
                return Task.FromResult(false);
            }

            //var result = await DebuggerSession.Instance.ExecuteOperation(() =>
            //{


            //    return Array.Empty<object>(); // this is the equivalent of "true" here because of the ExecuteOperation wrapper
            //});
        }
    }
}