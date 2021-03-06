﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Api.Playback;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.DefaultTheme.Core.ViewModels;
using MediaBrowser.Theater.DefaultTheme.Home.ViewModels;
using MediaBrowser.Theater.Presentation;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.ItemDetails.ViewModels
{
    public class ItemsListViewModel
        : BaseViewModel, IItemDetailSection, IKnownSize
    {
        private const int TileHeight = 100;

        private readonly ItemsResult _itemsResult;
        private readonly IApiClient _apiClient;
        private readonly IImageManager _imageManager;
        private readonly IServerEvents _serverEvents;
        private readonly INavigator _navigator;
        private readonly IPlaybackManager _playbackManager;
        private readonly ImageType[] _preferredImageTypes;

        private bool _isVisible;

        public int SortOrder 
        {
            get { return 2; }
        }

        public Size Size
        {
            get
            {
                if (Items.Count == 0) {
                    return new Size();
                }

                return new Size(600, 700);
            }
        }

        public string Title { get; set; }

        public RangeObservableCollection<ItemTileViewModel> Items { get; private set; }

        public bool IsVisible
        {
            get { return _isVisible; }
            private set
            {
                if (Equals(_isVisible, value))
                {
                    return;
                }

                _isVisible = value;
                OnPropertyChanged();
            }
        }

        public ItemsListViewModel(ItemsResult itemsResult, IApiClient apiClient, IImageManager imageManager, IServerEvents serverEvents, INavigator navigator, IPlaybackManager playbackManager)
        {
            _itemsResult = itemsResult;
            _apiClient = apiClient;
            _imageManager = imageManager;
            _serverEvents = serverEvents;
            _navigator = navigator;
            _playbackManager = playbackManager;

            var itemType = itemsResult.Items.Length > 0 ? itemsResult.Items.First().Type : null;
            if (itemType == "Episode") {
                _preferredImageTypes = new[] { ImageType.Screenshot, ImageType.Thumb, ImageType.Art, ImageType.Primary };
            } else {
                _preferredImageTypes = new[] { ImageType.Backdrop, ImageType.Thumb, ImageType.Art };
            }

            Title = SelectHeader(itemsResult.Items.Length > 0 ? itemsResult.Items.First().Type : null);
            Items = new RangeObservableCollection<ItemTileViewModel>();
            LoadItems();
        }

        private void LoadItems()
        {
            IEnumerable<ItemTileViewModel> items = _itemsResult.Items.Select(i => new ItemListElementViewModel(_apiClient, _imageManager, _serverEvents, _navigator, _playbackManager, i) {
                DesiredImageHeight = TileHeight,
                PreferredImageTypes = _preferredImageTypes
            });

            Items.Clear();
            Items.AddRange(items);

            IsVisible = Items.Count > 0;
            OnPropertyChanged("Size");
        }

        internal static string SelectHeader(string itemType)
        {
            switch (itemType)
            {
                case "Series":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_SeriesHeader".Localize();
                case "Season":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_SeasonsHeader".Localize();
                case "Episode":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_EpisodesHeader".Localize();
                case "Artist":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_AlbumsHeader".Localize();
                case "Album":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_TracksHeader".Localize();
                case "Movie":
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_MoviesHeader".Localize();
                default:
                    return "MediaBrowser.Theater.DefaultTheme:Strings:DetailSection_ItemsHeader".Localize();
            }
        }
    }
}
