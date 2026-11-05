using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Xm6Control.Core;

namespace Xm6Control.App;

/// <summary>
/// Attached properties that bind a XAML element's user-visible string to a
/// localization key. Registered elements are re-applied when the language
/// changes, so the UI switches without restarting.
/// </summary>
public static class Loc
{
    private enum Target
    {
        Text,
        Content,
        Header,
        Placeholder,
        OnContent,
        OffContent,
    }

    private static readonly List<Entry> Entries = [];

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.Text)));

    public static readonly DependencyProperty ContentProperty = DependencyProperty.RegisterAttached(
        "Content",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.Content)));

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.RegisterAttached(
        "Header",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.Header)));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.Placeholder)));

    public static readonly DependencyProperty OnContentProperty = DependencyProperty.RegisterAttached(
        "OnContent",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.OnContent)));

    public static readonly DependencyProperty OffContentProperty = DependencyProperty.RegisterAttached(
        "OffContent",
        typeof(string),
        typeof(Loc),
        new PropertyMetadata(null, (target, args) => OnChanged(target, args, Target.OffContent)));

    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetContent(DependencyObject element, string value) => element.SetValue(ContentProperty, value);

    public static string? GetContent(DependencyObject element) => (string?)element.GetValue(ContentProperty);

    public static void SetHeader(DependencyObject element, string value) => element.SetValue(HeaderProperty, value);

    public static string? GetHeader(DependencyObject element) => (string?)element.GetValue(HeaderProperty);

    public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);

    public static string? GetPlaceholder(DependencyObject element) => (string?)element.GetValue(PlaceholderProperty);

    public static void SetOnContent(DependencyObject element, string value) => element.SetValue(OnContentProperty, value);

    public static string? GetOnContent(DependencyObject element) => (string?)element.GetValue(OnContentProperty);

    public static void SetOffContent(DependencyObject element, string value) => element.SetValue(OffContentProperty, value);

    public static string? GetOffContent(DependencyObject element) => (string?)element.GetValue(OffContentProperty);

    /// <summary>Re-applies every registered string in the current language.</summary>
    public static void RefreshAll()
    {
        for (var index = Entries.Count - 1; index >= 0; index--)
        {
            var entry = Entries[index];
            if (!entry.Element.TryGetTarget(out var element))
            {
                Entries.RemoveAt(index);
                continue;
            }

            Apply(element, entry.Key, entry.Target);
        }
    }

    private static void OnChanged(
        DependencyObject element,
        DependencyPropertyChangedEventArgs args,
        Target target)
    {
        if (args.NewValue is not string key || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        Entries.RemoveAll(entry =>
            entry.Target == target
            && entry.Element.TryGetTarget(out var existing)
            && ReferenceEquals(existing, element));

        Entries.Add(new Entry(new WeakReference<DependencyObject>(element), key, target));
        Apply(element, key, target);
    }

    private static void Apply(DependencyObject element, string key, Target target)
    {
        var text = Localization.Text(key);
        switch (target)
        {
            case Target.Text:
                if (element is TextBlock textBlock)
                {
                    textBlock.Text = text;
                }

                break;

            case Target.Content:
                switch (element)
                {
                    case ButtonBase button:
                        button.Content = text;
                        break;
                    case NavigationViewItem navItem:
                        navItem.Content = text;
                        break;
                }

                break;

            case Target.Header:
                switch (element)
                {
                    case ToggleSwitch toggle:
                        toggle.Header = text;
                        break;
                    case ComboBox combo:
                        combo.Header = text;
                        break;
                    case TextBox box:
                        box.Header = text;
                        break;
                    case RadioButtons radio:
                        radio.Header = text;
                        break;
                }

                break;

            case Target.Placeholder:
                if (element is TextBox textBox)
                {
                    textBox.PlaceholderText = text;
                }

                break;

            case Target.OnContent:
                if (element is ToggleSwitch onToggle)
                {
                    onToggle.OnContent = text;
                }

                break;

            case Target.OffContent:
                if (element is ToggleSwitch offToggle)
                {
                    offToggle.OffContent = text;
                }

                break;
        }
    }

    private sealed record Entry(
        WeakReference<DependencyObject> Element,
        string Key,
        Target Target);
}
