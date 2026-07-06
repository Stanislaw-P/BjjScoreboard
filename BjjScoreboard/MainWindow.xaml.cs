using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace BjjScoreboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;

            if (e.Key == Key.Space)
            {
                var vm = this.DataContext as MainViewModel;
                if (vm != null)
                {
                    vm.TogglePauseCommand.Execute(null);
                }
                e.Handled = true;
            }

            // Переключение полноэкранного режима по нажатию F11
            if (e.Key == Key.F11)
            {
                if (this.WindowStyle == WindowStyle.None)
                {
                    // Возвращаем в обычное оконное состояние
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                    this.WindowState = WindowState.Normal;
                    this.ResizeMode = ResizeMode.CanResize;
                }
                else
                {
                    // Включаем чистый Fullscreen
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Maximized;
                    this.ResizeMode = ResizeMode.NoResize;

                    // --- ЛОГИКА ИСЧЕЗАЮЩЕГО УВЕДОМЛЕНИЯ ---
                    // 1. Показываем уведомление
                    FullscreenNotification.IsOpen = true;
                    FullscreenNotification.Child.Opacity = 1.0;

                    // 2. Создаем анимацию угасания (от 1.0 до 0.0 прозрачности)
                    DoubleAnimation fadeAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        BeginTime = TimeSpan.FromSeconds(2.5), // Ждем 2.5 секунды перед началом исчезновения
                        Duration = TimeSpan.FromSeconds(0.8)   // Само исчезновение длится 0.8 секунды
                    };

                    // 3. Когда анимация завершится, полностью закрываем Popup
                    fadeAnimation.Completed += (s, args) => { FullscreenNotification.IsOpen = false; };

                    // 4. Запускаем анимацию на внутреннем элементе Border
                    FullscreenNotification.Child.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                }
                e.Handled = true;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FocusManager.SetFocusedElement(this, null);
                Keyboard.ClearFocus();
                this.Focus(); // Передаем фокус окну, чтобы сразу работал пробел
                e.Handled = true;
            }
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                FocusManager.SetFocusedElement(this, null);
                Keyboard.ClearFocus();
                this.Focus(); // Передаем фокус окну, чтобы сразу работал пробел
            }
        }

        private void WeightTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры, минус и плюс
            bool isAllowed = char.IsDigit(e.Text[0]) || e.Text == "-" || e.Text == "+";

            // Блокируем ввод, если символ не разрешен
            e.Handled = !isAllowed;
        }
    }
}