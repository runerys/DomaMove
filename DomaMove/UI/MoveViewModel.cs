using System.Collections.Generic;
using System.Linq;
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

        protected override void OnInitialize()
        {
            DisplayName = "Transfer Doma Maps";
            _tracker.Startup();

            TransferMaps = new SelectionList<TransferMap>(new List<TransferMap>());

            base.OnInitialize();
        }

        protected override void OnDeactivate(bool close)
        {
            if (close)
            {
                _tracker.Shutdown();
            }

            base.OnDeactivate(close);
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

        public void GetMaps()
        {
            var waitCursor = WaitCursor.Start();

            Task.Factory.StartNew(() =>
                {
                    Task.WaitAll(Task.Factory.StartNew(() => Source.GetAllMaps()),
                                 Task.Factory.StartNew(() => Target.GetAllMaps()));

                    Target.TagAllExistingMapsAndSetTargetCategories(Source);
                })
                .ContinueWith(t =>
                    {
                        TransferMaps = new SelectionList<TransferMap>(Source.Maps);
                        waitCursor.Stop();
                        Summary = Source.GetSummary();
                        MapsArePrepared = Source.Maps.Count > 0;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void TransferAll()
        {
            TransferMaps.SelectAll();
            TransferSelected();
        }

        public void TransferSelected()
        {
            var selectedMaps = TransferMaps.SelectedItems.ToList();

            if (selectedMaps.Any())
            {
                if (AbortFromUserDialog(selectedMaps.Count))
                    return;

                var waitCursor = WaitCursor.Start();

                Task.Factory.StartNew(() =>
                    {
                        Target.UploadMaps(selectedMaps);
                        _tracker.TransferCompleted(Target.TransferSuccessCount, Target.TransferSuccessFailed);

                        foreach (var exception in Target.TransferExceptions)
                        {
                            _tracker.TransferException(exception);
                        }
                    })
                    .ContinueWith(t => waitCursor.Stop(), TaskScheduler.FromCurrentSynchronizationContext());
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
