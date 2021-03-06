﻿using System;
using System.Threading.Tasks;

namespace MediaBrowser.Theater.Api.Navigation
{
    public class NavigationEventArgs : EventArgs
    {
        public INavigationPath Path { get;set; }

        public NavigationEventArgs(INavigationPath path)
        {
            Path = path;
        }
    }

    /// <summary>
    ///     The INavigator interface defines the API for instructing a theme to
    ///     display the content relevant to a specified path.
    /// </summary>
    public interface INavigator
    {
        event EventHandler<NavigationEventArgs> Navigated;
        
        /// <summary>
        /// Initializes the navigator with an initial navigation context.
        /// </summary>
        /// <param name="rootContext">The root navigation context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Initialize(INavigationContext rootContext);
        
        /// <summary>
        ///     Gets the current location.
        /// </summary>
        INavigationPath CurrentLocation { get; }

        /// <summary>
        ///     Gets a value indicating if the theme can currently navigate back.
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        ///     Gets a value indicating if the them can currently navigate forward.
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        ///     Instructs the theme to display the content relevant to the given path.
        /// </summary>
        /// <param name="path">The path to navigate to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Navigate(INavigationPath path);

        /// <summary>
        ///     Navigates back to the previous location.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Back();

        /// <summary>
        ///     Navigates forward to the next location.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Forward();

        /// <summary>
        ///     Clears the navigation history.
        /// </summary>
        /// <param name="count">The number of items to remove from the history.</param>
        void ClearNavigationHistory(int count = int.MaxValue);
    }
}