﻿using System.Threading.Tasks;
using MediaBrowser.Theater.Api;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.DefaultTheme.Home.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.Home
{
    public class HomeContext
        : NavigationContext
    {
        private readonly ITheaterApplicationHost _appHost;
        private readonly IPresenter _presenter;

        private HomeViewModel _viewModel;

        public HomeContext(ITheaterApplicationHost appHost, IPresenter presenter) : base(appHost)
        {
            _appHost = appHost;
            _presenter = presenter;
        }

        public override async Task Activate()
        {
            if (_viewModel == null || !_viewModel.IsActive) {
                _viewModel = new HomeViewModel(_appHost);
            }

            await _presenter.ShowPage(_viewModel);
        }
    }
}