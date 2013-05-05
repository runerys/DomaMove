using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using DomaMove.Engine;
using DomaMove.Tracking;

namespace DomaMove.UI
{
    public class MoveViewModel : Screen
    {
        private readonly ITracker _tracker;

        public MoveViewModel(DomaConnection source, DomaConnection target, ITracker tracker)
        {
            _tracker = tracker;

            Source = source;
            Target = target;            
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
                Source.SaveConnectionParameters();
                Target.SaveConnectionParameters();
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

        public void GetMaps()
        {
            var waitCursor = WaitCursor.Start();

            Task.Factory.StartNew(() =>
                {
                    Task.WaitAll(Task.Factory.StartNew(() => Source.GetAllMaps()),
                                Task.Factory.StartNew(() => Target.GetAllMaps()));

                    Target.TagAllExistingMapsAndSetTargetCategories(Source);
                })
                .ContinueWith(t => Execute.OnUIThread(waitCursor.Stop));
        }

        public List<SourceMap> SelectedMaps { get; set; }

        public void Transfer()
        {
            var window = GetView() as Window;

            if (window == null)
                return;

            IList selectedItems = ((MoveView)window.Content).Source_Maps.SelectedItems;

            SelectedMaps = new List<SourceMap>();

            foreach (var selectedItem in selectedItems)
            {
                SelectedMaps.Add(selectedItem as SourceMap);
            }

            if (SelectedMaps != null && SelectedMaps.Any())
            {
                var waitCursor = WaitCursor.Start();

                Task.Factory.StartNew(() => Target.UploadMaps(SelectedMaps))
                            .ContinueWith(t => Execute.OnUIThread(waitCursor.Stop));
            }
        }
    }
}
