// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4: a duplicate sibling is told apart;
/// codex PR 3 round 2 and the spec review, round 21): the collision-aware
/// name of an items host's containers, the same rule for every host.
///
/// The host declares the property each item is read by
/// (<see cref="NamePathProperty"/>; "" reads the item itself), optionally
/// the one that tells two namesakes apart
/// (<see cref="DistinguisherPathProperty"/> — a path, a folder), and the
/// noun an ordinal takes (<see cref="NounProperty"/>: "item", "tab"). Its
/// container style binds each container's AutomationProperties.Name to the
/// container itself through <see cref="Converter"/>, which reads the name
/// <see cref="Compose"/> gave that container's item among ALL the host's
/// items — never only the realized ones, so a name does not depend on the
/// scroll position. A tree scopes per level: every tree item inherits the
/// declaration and names its own children.
/// </summary>
internal static class SiblingNames
{
    public static readonly DependencyProperty NamePathProperty = DependencyProperty.RegisterAttached(
        "NamePath",
        typeof(string),
        typeof(SiblingNames),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnDeclarationChanged));

    public static readonly DependencyProperty DistinguisherPathProperty = DependencyProperty.RegisterAttached(
        "DistinguisherPath",
        typeof(string),
        typeof(SiblingNames),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnDeclarationChanged));

    public static readonly DependencyProperty NounProperty = DependencyProperty.RegisterAttached(
        "Noun",
        typeof(string),
        typeof(SiblingNames),
        new FrameworkPropertyMetadata("item", FrameworkPropertyMetadataOptions.Inherits, OnDeclarationChanged));

    private static readonly DependencyProperty ScopeProperty = DependencyProperty.RegisterAttached(
        "Scope", typeof(Scope), typeof(SiblingNames), new PropertyMetadata(null));

    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Properties = new();

    /// <summary>Every live scope, for a container no host has linked (see
    /// <see cref="NameForUnlinked"/>); shared across UI threads, so
    /// locked.</summary>
    private static readonly List<WeakReference<Scope>> Scopes = [];

    private static readonly object ScopesLock = new();

    /// <summary>The containers whose item changes are watched (see
    /// <see cref="Watch"/>).</summary>
    private static readonly ConditionalWeakTable<FrameworkElement, object?> Watched = new();

    /// <summary>The container style's Name binding reads through this:
    /// <c>{Binding RelativeSource={RelativeSource Self}, Converter={x:Static
    /// local:SiblingNames.Converter}}</c>.</summary>
    public static IValueConverter Converter { get; } = new ContainerNameConverter();

    public static string? GetNamePath(DependencyObject element) => (string?)element.GetValue(NamePathProperty);

    public static void SetNamePath(DependencyObject element, string? value) => element.SetValue(NamePathProperty, value);

    public static string? GetDistinguisherPath(DependencyObject element) =>
        (string?)element.GetValue(DistinguisherPathProperty);

    public static void SetDistinguisherPath(DependencyObject element, string? value) =>
        element.SetValue(DistinguisherPathProperty, value);

    public static string GetNoun(DependencyObject element) => (string)element.GetValue(NounProperty);

    public static void SetNoun(DependencyObject element, string value) => element.SetValue(NounProperty, value);

    /// <summary>The container style a code-built host uses.</summary>
    internal static Style ContainerStyle(Type containerType)
    {
        var style = new Style(containerType);
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, ContainerNameBinding()));
        return style;
    }

    /// <summary>A container's Name through the rule — for a code-built
    /// container style that sets more than its Name.</summary>
    internal static Binding ContainerNameBinding() =>
        new() { RelativeSource = RelativeSource.Self, Converter = Converter };

    /// <summary>A stop inside a LAYOUT host's item template takes the name
    /// its (layout) container carries.</summary>
    internal static Binding FromContainer() =>
        new()
        {
            Path = new PropertyPath(AutomationProperties.NameProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContentPresenter), 1),
        };

    /// <summary>
    /// The rule, over sibling names in order and what tells each apart:
    /// <list type="number">
    /// <item>the item's name — a blank one reads "{Noun} {n}", never
    /// nothing;</item>
    /// <item>where siblings share it (ignoring case, as speech does), and
    /// the item has a distinguisher: "{name}, {distinguisher}";</item>
    /// <item>where names still collide — two namesakes with one
    /// distinguisher (one file in two tabs), or a suffixed name that meets a
    /// natural one — every member of the colliding group reads "{name},
    /// {noun} {n}", n its 1-based place;</item>
    /// <item>and should even those collide, "{Noun} {n}", unique by n.</item>
    /// </list>
    /// </summary>
    internal static string[] Compose(
        IReadOnlyList<string?> names, IReadOnlyList<string?> distinguishers, string noun)
    {
        int count = names.Count;
        string capitalized = noun.Length == 0
            ? "Item"
            : char.ToUpper(noun[0], CultureInfo.CurrentCulture) + noun[1..];
        string Ordinal(int index) => string.Create(CultureInfo.InvariantCulture, $"{capitalized} {index + 1}");
        string[] bases = new string[count];
        for (int index = 0; index < count; index++)
        {
            bases[index] = string.IsNullOrWhiteSpace(names[index]) ? Ordinal(index) : names[index]!;
        }
        string[] result = (string[])bases.Clone();
        foreach (int index in Colliding(result))
        {
            if (index < distinguishers.Count && !string.IsNullOrWhiteSpace(distinguishers[index]))
            {
                result[index] = $"{bases[index]}, {distinguishers[index]}";
            }
        }
        foreach (int index in Colliding(result))
        {
            result[index] = string.Create(
                CultureInfo.InvariantCulture, $"{bases[index]}, {noun} {index + 1}");
        }
        for (int pass = 0; pass <= count && Colliding(result) is { Count: > 0 } colliding; pass++)
        {
            foreach (int index in colliding)
            {
                result[index] = Ordinal(index);
            }
        }
        return result;
    }

    private static List<int> Colliding(string[] names) =>
        [
            .. Enumerable.Range(0, names.Length)
                .GroupBy(index => names[index], StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Skip(1).Any())
                .SelectMany(group => group)
                .Order(),
        ];

    /// <summary>A host's own declaration makes it a scope, and so does a
    /// tree item's inherited one (it names its children); any other items
    /// host that merely inherits one — a list inside a row's template — is
    /// no scope.</summary>
    private static void OnDeclarationChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ItemsControl host)
        {
            return;
        }
        BaseValueSource source = DependencyPropertyHelper.GetValueSource(host, NamePathProperty).BaseValueSource;
        bool declared = source is not (BaseValueSource.Inherited or BaseValueSource.Default or BaseValueSource.Unknown);
        if (GetNamePath(host) is null || !(declared || host is TreeViewItem))
        {
            return;
        }
        var scope = (Scope?)host.GetValue(ScopeProperty);
        if (scope is null)
        {
            scope = new Scope(host);
            host.SetValue(ScopeProperty, scope);
        }
        scope.Invalidate();
    }

    /// <summary>How many times <paramref name="host"/>'s names have been
    /// refreshed; -1 when it declares no scope.</summary>
    internal static int RefreshesForTests(ItemsControl host) =>
        host.GetValue(ScopeProperty) is Scope scope ? scope.Refreshes : -1;

    /// <summary>The name a container's item reads among its siblings, or
    /// null when its host declares no scope.</summary>
    internal static string? NameFor(DependencyObject container)
    {
        ItemsControl? host = ItemsControl.ItemsControlFromItemContainer(container);
        return host?.GetValue(ScopeProperty) is Scope scope ? scope.NameFor(container) : null;
    }

    /// <summary>
    /// The name of an item whose container no host has linked. UIA names an
    /// item no container holds — a closed combo's selection, a row a
    /// virtualized list has not realized — through a throwaway wrapper: a
    /// fresh container handed the item (its DataContext) and the host's
    /// container style, but placed in no panel, so no host can be found from
    /// it (the Bases and Graph inspector journeys heard the item's record
    /// dump). Its host is the scope, on this thread, whose container style it
    /// wears and whose items hold its item; null when none does.
    /// </summary>
    private static string? NameForUnlinked(DependencyObject container)
    {
        if (container is not FrameworkElement { DataContext: { } item, Style: { } style })
        {
            return null;
        }
        Scope[] scopes;
        lock (ScopesLock)
        {
            _ = Scopes.RemoveAll(reference => !reference.TryGetTarget(out _));
            scopes = [.. Scopes.Select(reference => reference.TryGetTarget(out Scope? scope) ? scope : null).OfType<Scope>()];
        }
        foreach (Scope scope in scopes)
        {
            if (scope.Host.CheckAccess()
                && ReferenceEquals(scope.Host.ItemContainerStyle, style)
                && scope.NameForItem(item) is { } name)
            {
                return name;
            }
        }
        return null;
    }

    /// <summary>A container the rule has named reads again whenever it is
    /// handed another item: UIA's wrapper is reused item after item, and a
    /// recycling panel re-links its containers — the name binding's source
    /// is the container itself, which never changes.</summary>
    private static void Watch(DependencyObject container)
    {
        if (container is FrameworkElement element)
        {
            _ = Watched.GetValue(element, key =>
            {
                key.DataContextChanged += (_, _) =>
                    BindingOperations.GetBindingExpression(key, AutomationProperties.NameProperty)?.UpdateTarget();
                return null;
            });
        }
    }

    internal static string Read(object? item, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return item as string ?? item?.ToString() ?? string.Empty;
        }
        if (item is IDictionary<string, object?> fields)
        {
            return fields.TryGetValue(path, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
        }
        if (item is null)
        {
            return string.Empty;
        }
        PropertyInfo? property = Properties.GetOrAdd(
            (item.GetType(), path),
            key => key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance));
        return property?.GetValue(item)?.ToString() ?? string.Empty;
    }

    /// <summary>One host's names: recomputed when its items or their names
    /// change, and pushed to its realized containers (their binding re-reads
    /// through <see cref="Converter"/>); a container realized later reads
    /// them when its binding first evaluates, and a container a recycling
    /// panel hands to another item reads again once that pass ends.</summary>
    private sealed class Scope
    {
        private readonly ItemsControl _host;
        private readonly HashSet<INotifyPropertyChanged> _observed = new(ReferenceEqualityComparer.Instance);
        private readonly ConditionalWeakTable<DependencyObject, object?> _known = new();
        private readonly List<WeakReference<DependencyObject>> _readers = [];
        private string[]? _names;
        private bool _refreshPending;

        internal Scope(ItemsControl host)
        {
            _host = host;
            lock (ScopesLock)
            {
                Scopes.Add(new WeakReference<Scope>(this));
            }
            ((INotifyCollectionChanged)host.Items).CollectionChanged += (_, _) => Invalidate();
            // A recycling panel (the Files tree, the filter results) re-links
            // a container to another item without re-applying its style, and
            // the name binding's source is the container itself, which never
            // changes: every container that has read a name reads again once
            // the generation pass completes, at its new place.
            host.ItemContainerGenerator.StatusChanged += (_, _) =>
            {
                if (_host.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    RefreshReaders();
                }
            };
        }

        internal int Refreshes { get; private set; }

        internal ItemsControl Host => _host;

        /// <summary>The name <paramref name="item"/> reads among the host's
        /// items (its first place, should it be there twice), or null when
        /// it is none of them.</summary>
        internal string? NameForItem(object item)
        {
            int index = _host.Items.IndexOf(item);
            if (index < 0)
            {
                return null;
            }
            _names ??= Compute();
            return index < _names.Length ? _names[index] : null;
        }

        /// <summary>The items or a name changed: the names are recomputed at
        /// their next read, and the realized containers read again once the
        /// current batch of changes is done — a folder resorted row by row, or
        /// five hundred filter results added one at a time, is ONE refresh
        /// (each reads every item, so one per change would be
        /// quadratic).</summary>
        internal void Invalidate()
        {
            _names = null;
            if (_refreshPending)
            {
                return;
            }
            _refreshPending = true;
            _ = _host.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(Refresh));
        }

        private void Refresh()
        {
            _refreshPending = false;
            Refreshes++;
            Observe();
            // By index: two equal items (one warning twice) are two
            // containers, which a lookup by item would conflate.
            for (int index = 0; index < _host.Items.Count; index++)
            {
                if (_host.ItemContainerGenerator.ContainerFromIndex(index) is DependencyObject container)
                {
                    BindingOperations.GetBindingExpression(container, AutomationProperties.NameProperty)?.UpdateTarget();
                }
            }
        }

        /// <summary>Every container that has read a name reads again — by
        /// container, never by item: two equal items are two
        /// containers.</summary>
        private void RefreshReaders()
        {
            for (int index = _readers.Count - 1; index >= 0; index--)
            {
                if (_readers[index].TryGetTarget(out DependencyObject? container))
                {
                    BindingOperations.GetBindingExpression(container, AutomationProperties.NameProperty)?.UpdateTarget();
                }
                else
                {
                    _readers.RemoveAt(index);
                }
            }
        }

        internal string? NameFor(DependencyObject container)
        {
            if (!_known.TryGetValue(container, out _))
            {
                _known.Add(container, null);
                _readers.Add(new WeakReference<DependencyObject>(container));
            }
            int index = _host.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0)
            {
                return null;
            }
            _names ??= Compute();
            return index < _names.Length ? _names[index] : null;
        }

        private string[] Compute()
        {
            string? namePath = GetNamePath(_host);
            string? distinguisherPath = GetDistinguisherPath(_host);
            var names = new List<string?>(_host.Items.Count);
            var distinguishers = new List<string?>(_host.Items.Count);
            foreach (object item in _host.Items)
            {
                names.Add(Read(item, namePath));
                distinguishers.Add(distinguisherPath is null ? null : Read(item, distinguisherPath));
            }
            return Compose(names, distinguishers, GetNoun(_host));
        }

        /// <summary>A renamed item re-names its siblings too: each item that
        /// notifies is watched, weakly, while it is one of the host's.</summary>
        private void Observe()
        {
            var current = new HashSet<INotifyPropertyChanged>(
                _host.Items.OfType<INotifyPropertyChanged>(), ReferenceEqualityComparer.Instance);
            foreach (INotifyPropertyChanged gone in _observed.Where(item => !current.Contains(item)).ToList())
            {
                PropertyChangedEventManager.RemoveHandler(gone, OnItemChanged, string.Empty);
                _ = _observed.Remove(gone);
            }
            foreach (INotifyPropertyChanged item in current.Where(item => !_observed.Contains(item)).ToList())
            {
                PropertyChangedEventManager.AddHandler(item, OnItemChanged, string.Empty);
                _ = _observed.Add(item);
            }
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName)
                || args.PropertyName == GetNamePath(_host)
                || args.PropertyName == GetDistinguisherPath(_host))
            {
                Invalidate();
            }
        }
    }

    private sealed class ContainerNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DependencyObject container)
            {
                return DependencyProperty.UnsetValue;
            }
            Watch(container);
            return NameFor(container) ?? NameForUnlinked(container) ?? DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

/// <summary>
/// W7-7 PR 3 (#1246, R-4): one string of a list whose strings may repeat —
/// a warning, a notice. WPF keys an items host's automation peers by item,
/// so two EQUAL strings get one peer between them: the list reads as one
/// row short and the second is unreachable, whatever its container is
/// named. Wrapped, each string is its own item, and its host names it by
/// <see cref="Text"/> under <see cref="SiblingNames"/> — the second of two
/// equal warnings reads "…, warning 2".
/// </summary>
internal sealed class SiblingText(string text)
{
    /// <summary>For an authored ItemsSource:
    /// <c>Converter={x:Static local:SiblingText.Rows}</c>.</summary>
    public static IValueConverter Rows { get; } = new RowsConverter();

    public string Text { get; } = text;

    internal static SiblingText[] Wrap(IEnumerable<string> texts) => [.. texts.Select(text => new SiblingText(text))];

    /// <summary>What a row shows with no template, and what a name that
    /// fell back to the item would read: the text, never this type's
    /// name.</summary>
    public override string ToString() => Text;

    private sealed class RowsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is IEnumerable<string> texts ? Wrap(texts) : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
