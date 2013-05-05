using System;
using System.ComponentModel;

namespace DomaMove.Wpf
{
    public class SelectionItem<T> : INotifyPropertyChanged
    {
        #region Fields

        private bool _isSelected;
        private T _item;

        #endregion

        #region Properties

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value == _isSelected) return;
                _isSelected = value;
                OnPropertyChanged("IsSelected");
                OnSelectionChanged();
            }
        }

        public T Item
        {
            get { return _item; }
            set
            {
                if (value.Equals(_item)) return;
                _item = value;
                OnPropertyChanged("Item");
            }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler SelectionChanged;

        #endregion

        #region ctor

        public SelectionItem(T item)
            : this(false, item)
        {
        }

        public SelectionItem(bool selected, T item)
        {
            _isSelected = selected;
            _item = item;
        }

        #endregion

        #region Event invokers

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler changed = PropertyChanged;
            if (changed != null) changed(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnSelectionChanged()
        {
            EventHandler changed = SelectionChanged;
            if (changed != null) changed(this, EventArgs.Empty);
        }

        #endregion
    }
}
