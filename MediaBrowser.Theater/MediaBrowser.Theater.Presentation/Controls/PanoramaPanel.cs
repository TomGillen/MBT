﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MediaBrowser.Theater.Presentation.Controls
{
    public interface IVirtualizedPanel
    {
        void MakeItemVisible(object item);
        Rect GetItemLocation(object item);
    }

    public interface IKnownSize
    {
        Size Size { get; }
    }

    public enum NavigationOptimizationHint
    { 
        None,
        NoSubItemNavigation
    }

    public class PanoramaPanel : VirtualizingPanel, IScrollInfo, IVirtualizedPanel
    {
        private const double LineSize = 16;
        private const double WheelSize = 3*LineSize;
        private static readonly Size InfiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

        private readonly TimeSpan _animationTime = TimeSpan.FromMilliseconds(250);
        private readonly Dictionary<int, DateTime> _lastVisibleTimes = new Dictionary<int, DateTime>();
        private readonly TimeSpan _removalDelay = TimeSpan.FromMilliseconds(600);
        private readonly TranslateTransform _transform;
        private readonly List<UIElement> _toBeArranged;

        private Size _extent;
        private int _firstVisibleItem;
        private double _itemOffAxisExtent;
        private double[] _itemPositionOffsets;
        private int _lastVisibleItem;
        private Vector _offset;
        private int _previousFirstVisibleItem;
        private Size _viewport;
        private Size _previousSize;

        private DateTime? _directionKeyPressed;
        private bool _allowSmoothScrolling;



        public NavigationOptimizationHint NavigationOptimizationHint
        {
            get { return (NavigationOptimizationHint)GetValue(NavigationOptimizationHintProperty); }
            set { SetValue(NavigationOptimizationHintProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NavigationOptimizationHint.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NavigationOptimizationHintProperty = 
    DependencyProperty.Register("NavigationOptimizationHint", typeof(NavigationOptimizationHint), typeof(PanoramaPanel), new PropertyMetadata(NavigationOptimizationHint.None));



        public PanoramaPanel()
        {
            _transform = new TranslateTransform();
            _toBeArranged = new List<UIElement>();
            RenderTransform = _transform;
        }

        private void EnableSmoothScrolling()
        {
            if (!_allowSmoothScrolling) {
                _allowSmoothScrolling = true;

                if (_transform.X != _offset.X) {
                    _transform.BeginAnimation(TranslateTransform.XProperty, GetAnimation(-_offset.X), HandoffBehavior.SnapshotAndReplace);
                }

                if (_transform.Y != _offset.Y) {
                    _transform.BeginAnimation(TranslateTransform.YProperty, GetAnimation(-_offset.Y), HandoffBehavior.SnapshotAndReplace);
                }
            }
        }

        private void DisableSmoothScrolling()
        {
            _allowSmoothScrolling = false;
        }

        private DateTime _lastHandledNavigation;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_directionKeyPressed == null) {
                _directionKeyPressed = DateTime.Now;
            } else if (DateTime.Now - _directionKeyPressed > TimeSpan.FromSeconds(0.75)) {
                DisableSmoothScrolling();
            }

            if (e.Key == Key.Left || e.Key == Key.Right) {
                if (NavigationOptimizationHint == Controls.NavigationOptimizationHint.None) {
                    PreventNavigationWrappingWithSubNavigation(e);
                } else {
                    PreventNavigationWrapping(e);
                }
            }

            if (!e.Handled) {
                base.OnKeyDown(e);
            }
        }

        private int FindChildIndex(FrameworkElement element)
        {
            for (var control = element; control != null; control = VisualTreeHelper.GetParent(control) as FrameworkElement) {
                var childIndex = Children.IndexOf(control);
                if (childIndex != -1) {
                    return childIndex;
                }
            }

            return -1;
        }

        private void PreventNavigationWrappingWithSubNavigation(KeyEventArgs e)
        {
            var currentFocus = Keyboard.FocusedElement as FrameworkElement;
            int childIndex = FindChildIndex(currentFocus);

            if (childIndex != 0 && childIndex != Children.Count - 1) {
                return;
            }

            if (e.Key == Key.Left && currentFocus != null) {
                var nextFocus = currentFocus.PredictFocus(FocusNavigationDirection.Left) as FrameworkElement;
                if (nextFocus != null) {
                    var currentPosition = currentFocus.TransformToAncestor(this).Transform(new Point(currentFocus.ActualWidth, 0));
                    var position = nextFocus.TransformToAncestor(this).Transform(new Point(0, 0));
                    if (position.X > currentPosition.X) {
                        e.Handled = true;
                    }
                }
            }

            if (e.Key == Key.Right && currentFocus != null) {
                var nextFocus = currentFocus.PredictFocus(FocusNavigationDirection.Right) as FrameworkElement;
                if (nextFocus != null) {
                    var currentPosition = currentFocus.TransformToAncestor(this).Transform(new Point(0, 0));
                    var position = nextFocus.TransformToAncestor(this).Transform(new Point(nextFocus.ActualWidth, 0));
                    if (position.X < currentPosition.X) {
                        e.Handled = true;
                    }
                }
            }
        }

        private void PreventNavigationWrapping(KeyEventArgs e)
        {
            var currentFocus = Keyboard.FocusedElement as FrameworkElement;
            var childIndex = FindChildIndex(currentFocus);

            // case where control to the left has not loaded yet, or we are the first item
            if (e.Key == Key.Left && childIndex == 0) {
                e.Handled = true;
            }

            // case where control to the right has not loaded yet, or we are the last item
            if (e.Key == Key.Right && childIndex == Children.Count - 1) {
                e.Handled = true;
            }
        }

        protected override async void OnKeyUp(KeyEventArgs e)
        {
            _directionKeyPressed = null;
            EnableSmoothScrolling();

            base.OnKeyUp(e);

            await Task.Delay(_removalDelay).ConfigureAwait(false);
            Dispatcher.BeginInvoke((Action) CleanUpItems, DispatcherPriority.Background);
        }

        public double StartScrollPadding { get; set; }
        public double EndScrollPadding { get; set; }

        public ScrollViewer ScrollOwner { get; set; }

        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll { get; set; }

        public double ExtentHeight
        {
            get { return _extent.Height; }
        }

        public double ExtentWidth
        {
            get { return _extent.Width; }
        }

        public double HorizontalOffset
        {
            get { return _offset.X; }
        }

        public double VerticalOffset
        {
            get { return _offset.Y; }
        }

        public double ViewportHeight
        {
            get { return _viewport.Height; }
        }

        public double ViewportWidth
        {
            get { return _viewport.Width; }
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (rectangle.IsEmpty || visual == null
                || visual == this || !base.IsAncestorOf(visual)) {
                return Rect.Empty;
            }

            rectangle = visual.TransformToAncestor(this).TransformBounds(rectangle);

            return MakeRectVisible(rectangle);
        }

        public void SetHorizontalOffset(double offset)
        {
            SetHorizontalOffset(offset, true);
        }

        public void SetHorizontalOffset(double offset, bool animate)
        {
            offset = Math.Max(0, Math.Min(offset, ExtentWidth - ViewportWidth));
            if (offset != _offset.X) {
                _offset.X = offset;

                if (animate && _allowSmoothScrolling) {
                    _transform.BeginAnimation(TranslateTransform.XProperty, GetAnimation(-offset), HandoffBehavior.SnapshotAndReplace);
                } else {
                    _transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-offset, TimeSpan.Zero), HandoffBehavior.SnapshotAndReplace);
                }

                InvalidateMeasure();
            }
        }

        public void SetVerticalOffset(double offset)
        {
            SetVerticalOffset(offset, true);
        }

        public void SetVerticalOffset(double offset, bool animate)
        {
            offset = Math.Max(0, Math.Min(offset, ExtentHeight - ViewportHeight));
            if (offset != _offset.Y) {
                _offset.Y = offset;

                if (animate && _allowSmoothScrolling) {
                    _transform.BeginAnimation(TranslateTransform.YProperty, GetAnimation(-offset), HandoffBehavior.SnapshotAndReplace);
                } else {
                    _transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-offset, TimeSpan.Zero), HandoffBehavior.SnapshotAndReplace);
                }

                InvalidateMeasure();
            }
        }

        private void ResetScroll()
        {
            _offset = new Vector(0, 0);

            _transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.Zero), HandoffBehavior.SnapshotAndReplace);
            _transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.Zero), HandoffBehavior.SnapshotAndReplace);

            InvalidateMeasure();
        }

        public void MakeItemVisible(object item)
        {
            var location = GetItemLocation(item);
            SetHorizontalOffset(location.Left);
            SetVerticalOffset(location.Top);
        }

        public Rect GetItemLocation(object item)
        {
            ItemsControl itemsControl = ItemsControl.GetItemsOwner(this);
            int index = itemsControl.Items.IndexOf(item);

            if (index < 0) {
                return new Rect();
            }

            return new Rect(_itemPositionOffsets[index], 0, _itemPositionOffsets[index + 1] - _itemPositionOffsets[index], _itemOffAxisExtent);
        }

        private Rect MakeRectVisible(Rect rectangle)
        {
            double desiredWidth = CanHorizontallyScroll ? Math.Min(rectangle.Width*1.6, _viewport.Width) : rectangle.Width;
            double desiredHeight = CanVerticallyScroll ? Math.Min(rectangle.Height*1.6, _viewport.Height) : rectangle.Height;
            rectangle.Inflate(Math.Max(desiredWidth - rectangle.Width, 0)*0.5, Math.Max(desiredHeight - rectangle.Height, 0)*0.5);

            if (ExtentWidth - rectangle.Right <= EndScrollPadding + 1) {
                rectangle.Width += EndScrollPadding;
            }

            var viewRect = new Rect(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
            viewRect.X = CalculateNewScrollOffset(viewRect.Left,
                                                  viewRect.Right, rectangle.Left, rectangle.Right);
            viewRect.Y = CalculateNewScrollOffset(viewRect.Top,
                                                  viewRect.Bottom, rectangle.Top, rectangle.Bottom);
            
            SetHorizontalOffset(viewRect.X);
            SetVerticalOffset(viewRect.Y);
            rectangle.Intersect(viewRect);
            rectangle.X -= viewRect.X;
            rectangle.Y -= viewRect.Y;

            return rectangle;
        }

        /// <summary>
        ///     Gets the animation.
        /// </summary>
        /// <param name="toValue">To value.</param>
        /// <returns>DoubleAnimation.</returns>
        private DoubleAnimation GetAnimation(double toValue)
        {
            var animation = new DoubleAnimation(toValue, _animationTime) {
                AccelerationRatio = 0,
                DecelerationRatio = 1,
                AutoReverse = false
            };

            return animation;
        }

        private ItemsControl _itemsControl;
        private void CalculateItemSizes()
        {
            ItemsControl itemsControl = ItemsControl.GetItemsOwner(this);
            if (itemsControl != _itemsControl) {
                SetupItemsChanged(itemsControl);
            }

            _itemPositionOffsets = new double[itemsControl.Items.Count + 1];
            _itemPositionOffsets[0] = StartScrollPadding;

            _itemOffAxisExtent = 0;

            for (int i = 0; i < itemsControl.Items.Count; i++) {
                Size itemSize = MeasureItem(itemsControl.Items[i]);
                _itemPositionOffsets[i + 1] = _itemPositionOffsets[i] + itemSize.Width;
                _itemOffAxisExtent = Math.Max(_itemOffAxisExtent, itemSize.Height);
            }
        }

        private void SetupItemsChanged(ItemsControl itemsControl)
        {
            if (_itemsControl != null) {
                var items = (INotifyCollectionChanged) _itemsControl.Items;
                items.CollectionChanged -= items_CollectionChanged;
            }

            _itemsControl = itemsControl;

            if (_itemsControl != null) {
                var items = (INotifyCollectionChanged)_itemsControl.Items;
                items.CollectionChanged += items_CollectionChanged;
            }
        }

        void items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) {
                foreach (var item in _itemsControl.Items) {
                    var notify = item as INotifyPropertyChanged;
                    if (notify != null) {
                        notify.PropertyChanged += notify_PropertyChanged;
                    }
                }

                ResetScroll();
            } else {
                if (e.NewItems != null) {
                    foreach (var item in e.NewItems) {
                        var notify = item as INotifyPropertyChanged;
                        if (notify != null) {
                            notify.PropertyChanged += notify_PropertyChanged;
                        }
                    }
                }

                if (e.OldItems != null) {
                    foreach (var item in e.OldItems) {
                        var notify = item as INotifyPropertyChanged;
                        if (notify != null) {
                            notify.PropertyChanged -= notify_PropertyChanged;
                        }
                    }
                }
            }
        }

        void notify_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Size") {
                CalculateItemSizes();
                InvalidateMeasure();
            }
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            CalculateItemSizes();

            base.OnItemsChanged(sender, args);
        }

        private Size MeasureItem(object item)
        {
            var knownSize = item as IKnownSize;
            if (knownSize != null) {
                return knownSize.Size;
            }

            // hard coded at the moment
            //todo return first item size
            return new Size(200, 200);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_itemPositionOffsets == null) {
                CalculateItemSizes();
            }

            var sizeChanged = availableSize == _previousSize;

            int itemCount = _itemPositionOffsets.Length - 1;

            var totalSize = new Size(_itemPositionOffsets[_itemPositionOffsets.Length - 1] + EndScrollPadding, _itemOffAxisExtent);
            VerifyScrollData(availableSize, totalSize);

            if (itemCount > 0) {
                int firstVisibleItemIndex;
                int lastVisibleItemIndex;
                FindVisibleRange(out firstVisibleItemIndex, out lastVisibleItemIndex);

                UIElementCollection children = InternalChildren;
                IItemContainerGenerator generator = ItemContainerGenerator;

                // Get the generator position of the first visible data item
                GeneratorPosition startPos = generator.GeneratorPositionFromIndex(firstVisibleItemIndex);

                // Get index where we'd insert the child for this position. If the item is realized
                // (position.Offset == 0), it's just position.Index, otherwise we have to add one to
                // insert after the corresponding child
                int childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;

                using (generator.StartAt(startPos, GeneratorDirection.Forward, true)) {
                    for (int itemIndex = firstVisibleItemIndex; itemIndex <= lastVisibleItemIndex; ++itemIndex, ++childIndex) {
                        bool newlyRealized;

                        // Get or create the child
                        var child = generator.GenerateNext(out newlyRealized) as UIElement;
                        if (newlyRealized) {
                            // Figure out if we need to insert the child at the end or somewhere in the middle
                            if (childIndex >= Children.Count) {
                                AddInternalChild(child);
                            } else {
                                InsertInternalChild(childIndex, child);
                            }
                            generator.PrepareItemContainer(child);
                        }

                        // Measurements will depend on layout algorithm
                        if (sizeChanged) {
                            child.Measure(InfiniteSize);
                        }

                        if (newlyRealized || sizeChanged) {
                            _toBeArranged.Add(child);
                        }
                    }
                }

                EnqueueCleanup(firstVisibleItemIndex, lastVisibleItemIndex);
            }

            _previousSize = availableSize;

            return new Size(Math.Min(availableSize.Width, totalSize.Width), Math.Min(availableSize.Height, totalSize.Height));
        }

        private void EnqueueCleanup(int firstVisibleItemIndex, int lastVisibleItemIndex)
        {
            _firstVisibleItem = firstVisibleItemIndex;
            _lastVisibleItem = lastVisibleItemIndex;

            DateTime now = DateTime.Now;

            for (int i = _firstVisibleItem; i < _lastVisibleItem + 1; i++) {
                _lastVisibleTimes[i] = now;
            }

            CleanUpItems();
        }
        
        private void CleanUpItems()
        {
            UIElementCollection children = InternalChildren;
            IItemContainerGenerator generator = ItemContainerGenerator;

            DateTime now = DateTime.Now;

            for (int i = children.Count - 1; i >= 0; i--) {
                DateTime lastVisible;
                
                // Map a child index to an item index by going through a generator position
                var childGeneratorPos = new GeneratorPosition(i, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(childGeneratorPos);

                if (_lastVisibleTimes.TryGetValue(itemIndex, out lastVisible) && (now - lastVisible) < _removalDelay) {
                    continue;
                }

                if (itemIndex < _firstVisibleItem || itemIndex > _lastVisibleItem) {
                    generator.Remove(childGeneratorPos, 1);
                    RemoveInternalChildRange(i, 1);
                    _lastVisibleTimes.Remove(itemIndex);
                }
            }
        }

        private void FindVisibleRange(out int first, out int last)
        {
            int itemCount = _itemPositionOffsets.Length - 1;

            if (itemCount < 2) {
                first = 0;
                last = 0;
                return;
            }

            first = Math.Max(0, Math.Min(_previousFirstVisibleItem, itemCount - 1));
            if (_itemPositionOffsets[first + 1] < HorizontalOffset) {
                while (_itemPositionOffsets[first + 1] < HorizontalOffset && first < itemCount - 1) {
                    first++;
                }
            } else {
                while (_itemPositionOffsets[first] > HorizontalOffset && first > 0) {
                    first--;
                }
            }

            last = first;
            while (_itemPositionOffsets[last + 1] < HorizontalOffset + _viewport.Width && last < itemCount - 1) {
                last++;
            }

            if (_itemPositionOffsets[last + 1] - _itemPositionOffsets[first] <= _viewport.Width) {
                first = Math.Max(0, first - 1);
                last = Math.Min(itemCount - 1, last + 1);
            }

            _previousFirstVisibleItem = first;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children == null || Children.Count == 0) {
                return finalSize;
            }

            IItemContainerGenerator generator = ItemContainerGenerator;

            for (int index = 0; index < Children.Count; index++) {
                UIElement child = Children[index];

                if (_toBeArranged.Contains(child)) {
                    int itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(index, 0));
                    ArrangeChild(itemIndex, child);
                }
            }

            _toBeArranged.Clear();
            
            VerifyScrollData(finalSize, new Size(_itemPositionOffsets[_itemPositionOffsets.Length - 1] + EndScrollPadding, _itemOffAxisExtent));

            return finalSize;
        }

        private void ArrangeChild(int itemIndex, UIElement child)
        {
            child.Arrange(new Rect(_itemPositionOffsets[itemIndex], 0, //_itemPositionOffsets[itemIndex] - HorizontalOffset, -VerticalOffset,
                                   _itemPositionOffsets[itemIndex + 1] - _itemPositionOffsets[itemIndex], child.DesiredSize.Height));
        }

        private static double CalculateNewScrollOffset(double topView,
                                                       double bottomView, double topChild, double bottomChild)
        {
            bool offBottom = topChild < topView && bottomChild < bottomView;
            bool offTop = bottomChild > bottomView && topChild > topView;
            bool tooLarge = (bottomChild - topChild) > (bottomView - topView);

            if (!offBottom && !offTop) {
                return topView;
            } //Don't do anything, already in view

            if ((offBottom && !tooLarge) || (offTop && tooLarge)) {
                return topChild;
            }

            return (bottomChild - (bottomView - topView));
        }

        protected void VerifyScrollData(Size viewport, Size extent)
        {
            if (double.IsInfinity(viewport.Width)) {
                viewport.Width = extent.Width;
            }

            if (double.IsInfinity(viewport.Height)) {
                viewport.Height = extent.Height;
            }

            _extent = extent;
            _viewport = viewport;

            _offset.X = Math.Max(0, Math.Min(_offset.X, ExtentWidth - ViewportWidth));
            _offset.Y = Math.Max(0, Math.Min(_offset.Y, ExtentHeight - ViewportHeight));

            if (ScrollOwner != null) {
                ScrollOwner.InvalidateScrollInfo();
            }
        }

        #region Movement Methods

        public void LineDown()
        {
            SetVerticalOffset(VerticalOffset + LineSize);
        }

        public void LineUp()
        {
            SetVerticalOffset(VerticalOffset - LineSize);
        }

        public void LineLeft()
        {
            SetHorizontalOffset(HorizontalOffset - LineSize);
        }

        public void LineRight()
        {
            SetHorizontalOffset(HorizontalOffset + LineSize);
        }

        public void MouseWheelDown()
        {
            SetVerticalOffset(VerticalOffset + WheelSize);
        }

        public void MouseWheelUp()
        {
            SetVerticalOffset(VerticalOffset - WheelSize);
        }

        public void MouseWheelLeft()
        {
            SetHorizontalOffset(HorizontalOffset - WheelSize);
        }

        public void MouseWheelRight()
        {
            SetHorizontalOffset(HorizontalOffset + WheelSize);
        }

        public void PageDown()
        {
            SetVerticalOffset(VerticalOffset + ViewportHeight);
        }

        public void PageUp()
        {
            SetVerticalOffset(VerticalOffset - ViewportHeight);
        }

        public void PageLeft()
        {
            SetHorizontalOffset(HorizontalOffset - ViewportWidth);
        }

        public void PageRight()
        {
            SetHorizontalOffset(HorizontalOffset + ViewportWidth);
        }

        #endregion
    }
}