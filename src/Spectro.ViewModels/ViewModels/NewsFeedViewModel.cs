﻿using GalaSoft.MvvmLight;
using Spectro.DataModel;

namespace Spectro.ViewModels
{
    public class NewsFeedViewModel : ViewModelBase
    {
        public NewsFeedViewModel()
        {

        }

        public NewsFeed Episode { get; set; }

    }
}