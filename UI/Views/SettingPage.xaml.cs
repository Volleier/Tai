using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UI.ViewModels;

namespace UI.Views
{
    public partial class SettingPage : Page
    {
        private SettingPageVM _vm;

        public SettingPage(SettingPageVM vm)
        {
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateHighlight(false);
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingPageVM.IsExportCsv))
            {
                UpdateHighlight(true);
            }
        }

        private void OnFormatSwitchClick(object sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is string tag && bool.TryParse(tag, out bool isCsv))
            {
                _vm.IsExportCsv = isCsv;
            }
        }

        private void UpdateHighlight(bool animate)
        {
            double targetX = _vm.IsExportCsv ? 42 : 0;

            // 文字颜色 (TextBlock 在 Border 内)
            if (XlsxBtn.Child is TextBlock xlsxTb)
                xlsxTb.Foreground = _vm.IsExportCsv
                    ? (Brush)FindResource("DefaultTextBrush")
                    : Brushes.White;
            if (CsvBtn.Child is TextBlock csvTb)
                csvTb.Foreground = _vm.IsExportCsv
                    ? Brushes.White
                    : (Brush)FindResource("DefaultTextBrush");

            if (!animate) return;

            var anim = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromSeconds(0.22),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Highlight.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim);
        }
    }
}
