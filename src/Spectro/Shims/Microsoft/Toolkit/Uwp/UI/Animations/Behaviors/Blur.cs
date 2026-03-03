using Microsoft.Xaml.Interactivity;
using Windows.UI.Xaml;

namespace Microsoft.Toolkit.Uwp.UI.Animations.Behaviors
{
    public class Blur : DependencyObject, IBehavior
    {
        public DependencyObject AssociatedObject { get; private set; }

        public bool AutomaticallyStart
        {
            get => (bool)GetValue(AutomaticallyStartProperty);
            set => SetValue(AutomaticallyStartProperty, value);
        }

        public static readonly DependencyProperty AutomaticallyStartProperty =
            DependencyProperty.Register(nameof(AutomaticallyStart), typeof(bool), typeof(Blur), new PropertyMetadata(false));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(Blur), new PropertyMetadata(0d));

        public double Duration
        {
            get => (double)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(double), typeof(Blur), new PropertyMetadata(0d));

        public void Attach(DependencyObject associatedObject) => AssociatedObject = associatedObject;

        public void Detach() => AssociatedObject = null;
    }
}
