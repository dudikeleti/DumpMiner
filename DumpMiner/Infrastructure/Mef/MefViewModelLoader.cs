using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using DumpMiner.Common;
using DumpMiner.ViewModels;

namespace DumpMiner.Infrastructure.Mef
{
    [Export]
    public class MefViewModelLoader : IViewModelLoader
    {
        private readonly Dictionary<string, Dictionary<IHasViewModel, BaseViewModel>> _viewsAndViewModels = new Dictionary<string, Dictionary<IHasViewModel, BaseViewModel>>();

        [ImportMany]
        private IEnumerable<Lazy<BaseViewModel, IViewModelMetadata>> ViewModels { get; set; }

        public BaseViewModel Load(IHasViewModel viewVm)
        {
            BaseViewModel viewModel = (from vm in ViewModels
                                       where vm.Metadata.Name == viewVm.ViewModelName
                                       select vm.Value).FirstOrDefault();

            if (viewModel == null)
                throw new ArgumentException("Invalid name: " + viewVm.ViewModelName);

            BaseViewModel result;
            if (!_viewsAndViewModels.ContainsKey(viewVm.ViewModelName))
            {
                InitViewModelOperation(viewVm, viewModel);
                viewVm.IsViewModelLoaded = true;
                _viewsAndViewModels[viewVm.ViewModelName] = new Dictionary<IHasViewModel, BaseViewModel> { { viewVm, viewModel } };
            }

            if (viewVm.IsViewModelLoaded)
            {
                result = _viewsAndViewModels[viewVm.ViewModelName][viewVm];
            }
            else
            {
                var newViewModel = (BaseViewModel)Activator.CreateInstance(viewModel.GetType());
                InitViewModelOperation(viewVm, newViewModel);
                viewVm.IsViewModelLoaded = true;
                _viewsAndViewModels[viewVm.ViewModelName].Add(viewVm, newViewModel);
                result = newViewModel;
            }
            result.ViewModelDisposed += ViewModelDisposed;
            return result;
        }

        private void InitViewModelOperation(IHasViewModel viewVm, BaseViewModel viewModel)
        {
            BaseOperationViewModel vm;
            if (viewVm.ExtendedData != null && (vm = viewModel as BaseOperationViewModel) != null)
            {
                object obj;
                if (viewVm.ExtendedData.TryGetValue("OperationName", out obj))
                {
                    var operationName = obj as string;
                    if (!string.IsNullOrEmpty(operationName))
                    {
                        try
                        {
                            vm.Operation = App.Container.GetExportedValue<IDebuggerOperation>(operationName);
                        }
                        catch (ImportCardinalityMismatchException e)
                        {
                            App.Container.GetExport<IDialogService>().Value.ShowDialog(e.Message);
                        }
                    }
                }
            }
        }

        public void Unload<T>()
        {
            var vms = new List<BaseViewModel>(_viewsAndViewModels.Values.SelectMany(dic => dic.Values).Where(vm => vm is T));
            foreach (var value in vms)
            {
                value.Dispose();
            }
        }

        private void ViewModelDisposed(BaseViewModel vm)
        {
            Dictionary<IHasViewModel, BaseViewModel> value;
            _viewsAndViewModels.TryGetValue(vm.ViewModelName, out value);
            var result = value?.Keys.FirstOrDefault(key => value[key] == vm);
            if (result == null) return;
            result.IsViewModelLoaded = false;
            if (_viewsAndViewModels[vm.ViewModelName].Count == 1)
                _viewsAndViewModels.Remove(vm.ViewModelName);
            else
                _viewsAndViewModels[vm.ViewModelName].Remove(result);
        }
    }
}
