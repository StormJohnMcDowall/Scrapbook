using System.Windows;
using System.Windows.Controls;

namespace Scrapbook.Behaviors;

/// <summary>Makes PasswordBox.Password bindable (two-way) via PasswordBoxAssist.BoundPassword.</summary>
public static class PasswordBoxAssist
{
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword", typeof(string), typeof(PasswordBoxAssist),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
        "IsHooked", typeof(bool), typeof(PasswordBoxAssist), new PropertyMetadata(false));

    public static string? GetBoundPassword(DependencyObject d) => (string?)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string? value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;

        if (!(bool)box.GetValue(IsHookedProperty))
        {
            box.PasswordChanged += OnPasswordChanged;
            box.SetValue(IsHookedProperty, true);
        }

        var value = (string?)e.NewValue ?? "";
        if (box.Password != value) box.Password = value;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        if (GetBoundPassword(box) != box.Password) SetBoundPassword(box, box.Password);
    }
}
