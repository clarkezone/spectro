using Microsoft.Xaml.Interactivity;
using System.Windows.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace Cimbalino.Toolkit.Behaviors
{
    public class EnterKeyBehavior : DependencyObject, IBehavior
    {
        private UIElement _associatedObject;

        public DependencyObject AssociatedObject => _associatedObject;

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(EnterKeyBehavior), new PropertyMetadata(null));

        public void Attach(DependencyObject associatedObject)
        {
            _associatedObject = associatedObject as UIElement;
            if (_associatedObject != null)
            {
                _associatedObject.KeyDown += OnKeyDown;
            }
        }

        public void Detach()
        {
            if (_associatedObject != null)
            {
                _associatedObject.KeyDown -= OnKeyDown;
                _associatedObject = null;
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            if (Command?.CanExecute(null) == true)
            {
                Command.Execute(null);
            }
        }
    }
}
