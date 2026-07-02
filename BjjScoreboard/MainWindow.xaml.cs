using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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