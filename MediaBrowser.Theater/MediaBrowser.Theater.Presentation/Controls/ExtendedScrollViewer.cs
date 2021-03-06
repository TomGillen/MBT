﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaBrowser.Theater.Presentation.Controls
{
    /// <summary>
    /// This subclass solves the problem of ScrollViewers eating KeyDown for all arrow keys
    /// </summary>
    public class ExtendedScrollViewer : ScrollViewer
    {
        public bool IgnoreAllDirectionKeys { get; set; }

        /// <summary>
        /// Responds to specific keyboard input and invokes associated scrolling behavior.
        /// </summary>
        /// <param name="e">Required arguments for this event.</param>
        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled || e.OriginalSource.Equals(this))
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) {
                return;
            }
            
//            // Don't eat left/right if horizontal scrolling is disabled
//            if (e.Key == Key.Left || e.Key == Key.Right)
//            {
//                if (HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled || HorizontalOffset.Equals(0) || IgnoreAllDirectionKeys)
//                {
//                    return;
//                }
//            }
//
//            // Don't eat up/down if vertical scrolling is disabled
//            if (e.Key == Key.Up || e.Key == Key.Down)
//            {
//                if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled || VerticalOffset.Equals(0) || IgnoreAllDirectionKeys)
//                {
//                    return;
//                }
//            }

            // Let the base class do it's thing
            base.OnKeyDown(e);

            if (e.Key == Key.Home) {
                await Task.Delay(100);
                MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }

            if (e.Key == Key.End) {
                await Task.Delay(100);
                MoveFocus(new TraversalRequest(FocusNavigationDirection.Last));
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled && HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) {
                ScrollToHorizontalOffset(HorizontalOffset - e.Delta);
            } else {
                base.OnMouseWheel(e);
            }
        }
    }
}
