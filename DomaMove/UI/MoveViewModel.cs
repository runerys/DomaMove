using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public MoveViewModel(DomaConnection source, DomaConnection target, ITracker tracker)
        {
            _tracker = tracker;

            Source = source;
            Target = target;

            TransferMaps = new SelectionList<TransferMap>(new List<TransferMap>());
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _tracker.Startup();
        }

        protected override void OnDeactivate(bool close)
        {
            if (close)
            {
                _tracker.Shutdown();
            }

            base.OnDeactivate(close);
        }

        public DomaConnection Source { get; set; }
        public DomaConnection Target { get; set; }

        public void TestSourceConnection()
        {
            Source.TestConnection();
        }

        public void TestTargetConnection()
        {
            Target.TestConnection();
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
                    }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void Transfer()
        {
            var selectedMaps = TransferMaps.SelectedItems.ToList();

            if (selectedMaps.Any())
            {
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
    }
}
