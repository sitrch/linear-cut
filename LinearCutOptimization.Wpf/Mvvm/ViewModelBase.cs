using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LinearCutOptimization.Wpf.Mvvm
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            RaisePropertyChanged(propName);
            return true;
        }
    }
}