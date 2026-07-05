using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BjjScoreboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;

            // Назначаем фильтр веса
            WeightBox.PreviewTextInput += WeightTextBox_PreviewTextInput;
            WeightBox.KeyDown += TextBox_KeyDown;

            // Слушаем все текстовые поля во вложенных элементах
            EventManager.RegisterClassHandler(typeof(TextBox), TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown));
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;

            if (e.Key == Key.Space)
            {
                var vm = this.DataContext as MainViewModel;
                if (vm != null) vm.TogglePauseCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FocusManager.SetFocusedElement(this, null);
                Keyboard.ClearFocus();
                this.Focus();
                e.Handled = true;
            }
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                FocusManager.SetFocusedElement(this, null);
                Keyboard.ClearFocus();
                this.Focus();
            }
        }

        private void WeightTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            bool isAllowed = char.IsDigit(e.Text[0]) || e.Text == "-" || e.Text == "+";
            e.Handled = !isAllowed;
        }
    }
}