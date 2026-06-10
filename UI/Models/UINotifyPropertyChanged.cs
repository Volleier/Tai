using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UI.Models
{
    public class UINotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发 PropertyChanged 事件。
        /// 如果调用线程不是 UI 线程，自动通过 Dispatcher 调度到 UI 线程执行，
        /// 避免后台线程直接更新绑定属性导致的跨线程异常。
        /// </summary>
        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null) return;

            //  已在 UI 线程：直接触发
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                //  后台线程：通过 Dispatcher 调度到 UI 线程
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }
    }
}
