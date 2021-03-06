﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MediaBrowser.Theater.DefaultTheme.Osd.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.Osd.Views
{
    /// <summary>
    ///     Interaction logic for OsdView.xaml
    /// </summary>
    public partial class OsdView : UserControl
    {
        /// <summary>
        ///     The is position slider dragging
        /// </summary>
        private bool _isPositionSliderUpdating;

        /// <summary>
        ///     Event handler for mouse down, add via addhandler so we can catch handled events
        /// </summary>
        private MouseButtonEventHandler _previewMouseDown;

        public OsdView()
        {
            InitializeComponent();

            DataContextChanged += FullscreenVideoTransportOsd_DataContextChanged;
            Loaded += FullscreenVideoTransportOsd_Loaded;
            Unloaded += FullscreenVideoTransportOsd_Unloaded;
        }

        /// <summary>
        ///     Gets the view model.
        /// </summary>
        /// <value>The view model.</value>
        public OsdViewModel ViewModel
        {
            get { return DataContext as OsdViewModel; }
        }

        private void FullscreenVideoTransportOsd_Loaded(object sender, RoutedEventArgs e)
        {
            // The silder has IsMoveToPointEnabled=true, which means it moves the thumb when we click the mouse, which also set handled to true
            // so we can't get the event. We need to turn event on so we can tell the slider to ignore position tick property notifications until
            // we get a mouse up and update the slider position. So we use AddHanlder, so we can catch handled events
            if (_previewMouseDown == null) {
                _previewMouseDown = CurrentPositionSlider_PreviewMouseDown;
                CurrentPositionSlider.AddHandler(PreviewMouseDownEvent, _previewMouseDown, true);
            }

            PlayPauseButton.Focus();
        }

        private void FullscreenVideoTransportOsd_Unloaded(object sender, RoutedEventArgs e)
        {
            CurrentPositionSlider.RemoveHandler(PreviewMouseDownEvent, _previewMouseDown);
            _previewMouseDown = null;

            OsdViewModel vm = ViewModel;

            if (vm != null) {
                vm.PropertyChanged -= vm_PropertyChanged;
            }
        }

        private async void FullscreenVideoTransportOsd_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            OsdViewModel vm = ViewModel;

            if (vm != null) {
                vm.PropertyChanged += vm_PropertyChanged;
            }

            await Task.Delay(100);
            PlayPauseButton.Focus();
        }

        private void vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OsdViewModel vm = ViewModel;
            if (string.Equals(e.PropertyName, "PositionTicks") && vm != null) {
                if (!_isPositionSliderUpdating) {
                    CurrentPositionSlider.Value = vm.PositionTicks;
                }
            }
        }

//        private async void UpdateLogo(OsdViewModel viewModel, BaseItemDto media)
//        {
//            ImgLogo.Visibility = Visibility.Hidden;
//
//            if (media != null && (media.HasLogo || !string.IsNullOrEmpty(media.ParentLogoItemId)))
//            {
//                try
//                {
//                    ImgLogo.Source =
//                        await
//                            viewModel.ImageManager.GetRemoteBitmapAsync(viewModel.ApiClient.GetLogoImageUrl(media,
//                                new ImageOptions
//                                {
//                                }));
//
//                    ImgLogo.Visibility = Visibility.Visible;
//                }
//                catch
//                {
//                    // Already logged at lower levels
//                }
//            }
//        }

        /// <summary>
        ///     Handles the DragStarted event of the CurrentPositionSlider control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragStartedEventArgs" /> instance containing the event data.</param>
        private void CurrentPositionSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isPositionSliderUpdating = true;
        }

        /// <summary>
        ///     Handles the DragCompleted event of the CurrentPositionSlider control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragCompletedEventArgs" /> instance containing the event data.</param>
        private void CurrentPositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ViewModel.Seek(Convert.ToInt64(CurrentPositionSlider.Value));
            _isPositionSliderUpdating = false;
        }

        /// <summary>
        ///     Handles the PreviewMouseDown event of the CurrentPositionSlider control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs" /> instance containing the event data.</param>
        private void CurrentPositionSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isPositionSliderUpdating = true;
            try {
                ViewModel.Seek(Convert.ToInt64(CurrentPositionSlider.Value));
            }
            finally {
                _isPositionSliderUpdating = false;
            }
        }
    }
}