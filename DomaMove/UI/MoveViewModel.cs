﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using DomaMove.Engine;
using DomaMove.Tracking;
using DomaMove.Wpf;

namespace DomaMove.UI
{
    public class MoveViewModel : Screen
    {
        private readonly ITracker _tracker;
        private SelectionList<TransferMap> _transferMaps;
        private bool _mapsArePrepared;
        private string _summary;
        private bool _showSummary;

        public MoveViewModel(DomaConnection source, DomaConnection target, ITracker tracker)
        {
            _tracker = tracker;

            Source = source;
            Target = target;
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            DisplayName = "Transfer Doma Maps";
            _tracker.Startup();

            TransferMaps = new SelectionList<TransferMap>(new List<TransferMap>());

            await base.OnInitializeAsync(cancellationToken);
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
            {
                _tracker.Shutdown();
            }

            await base.OnDeactivateAsync(close, cancellationToken);
        }

        public string Summary
        {
            get { return _summary; }
            set
            {
                if (value == _summary) return;
                _summary = value;

                ShowSummary = !string.IsNullOrEmpty(_summary);

                NotifyOfPropertyChange("Summary");
            }
        }

        public bool ShowSummary
        {
            get { return _showSummary; }
            set
            {
                if (value.Equals(_showSummary)) return;
                _showSummary = value;
                NotifyOfPropertyChange("ShowSummary");
            }
        }

        public DomaConnection Source { get; set; }
        public DomaConnection Target { get; set; }

        public void TestSourceConnection()
        {
            var waitCursor = WaitCursor.Start();
            Source.TestConnection()
                  .ContinueWith(t => waitCursor.Stop(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void TestTargetConnection()
        {
            var waitCursor = WaitCursor.Start();
            Target.TestConnection()
                  .ContinueWith(t => waitCursor.Stop(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        public SelectionList<TransferMap> TransferMaps
        {
            get { return _transferMaps; }
            set
            {
                if (Equals(value, _transferMaps)) return;
                _transferMaps = value;
                NotifyOfPropertyChange("TransferMaps");
            }
        }

        public bool MapsArePrepared
        {
            get { return _mapsArePrepared; }
            set
            {
                if (value.Equals(_mapsArePrepared)) return;
                _mapsArePrepared = value;
                NotifyOfPropertyChange("MapsArePrepared");
            }
        }

        public async Task GetMaps()
        {
            var waitCursor = WaitCursor.Start();

            try
            {
                var sourceMaps = Source.GetAllMaps();
                var targetMaps = Target.GetAllMaps();

                await Task.WhenAll(sourceMaps, targetMaps);

                Target.TagAllExistingMapsAndSetTargetCategories(Source);

                TransferMaps = new SelectionList<TransferMap>(Source.Maps);
                Summary = Source.GetSummary();
                MapsArePrepared = Source.Maps.Count > 0;
            }           
            finally
            {
                waitCursor.Stop();
            }         
        }

        public async Task TransferAll()
        {
            TransferMaps.SelectAll();
            await TransferSelected();
        }

        public async Task TransferSelected()
        {
            var selectedMaps = TransferMaps.SelectedItems.ToList();

            if (selectedMaps.Any())
            {
                if (AbortFromUserDialog(selectedMaps.Count))
                    return;

                var waitCursor = WaitCursor.Start();

                try
                {
                    await Target.UploadMaps(selectedMaps);
                    _tracker.TransferCompleted(Target.TransferSuccessCount, Target.TransferSuccessFailed);

                    if (Target.TransferExceptions.Any()) 
                        _tracker.TransferException(Target.TransferExceptions);
                }
                finally 
                {
                    waitCursor.Stop(); 
                }               
            }
        }

        private bool AbortFromUserDialog(int count)
        {
            string text = string.Format("Ready to transfer {0} maps. Shall we proceed?", count);

            if (count == 1)
                text = "Ready to transfer a single map. Shall we proceed?";

            var result = MessageBox.Show(text, "Confirm transfer", MessageBoxButton.YesNo);

            return result != MessageBoxResult.Yes;
        }
    }
}
