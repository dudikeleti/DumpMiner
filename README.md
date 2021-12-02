# Updated version of DumpMiner (2021-12-02)

This fork fixes a couple of bugs and adds the ability to dump objects to file.

Right click any record that contains both an "Address" and "Size" column and the object's raw data will be written to a file in the current working directory, along with a text file providing some information about the dumped objects.

An extended feature is that `System.Drawing.Bitmap` objects can be dumped from 32-bit processes. Currently only objects that were dynamically created (e.g. via `new Bitmap(...)`) or loaded from a BMP file work, as the internal GDI+ structures are undocumented and I couldn't quite figure out how to get the raw pixel data for JPEG, PNG, etc. images. However, in the case that such a Bitmap object is found, it will be handled gracefully and the file path to the image will be reported.

