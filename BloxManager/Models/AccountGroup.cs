using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BloxManager.Models
{
    public class AccountGroup : AccountListItem
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private int _sortOrder;


        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
        }

        public int SortOrder
        {
            get => _sortOrder;
            set { if (_sortOrder != value) { _sortOrder = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<Account> Accounts { get; } = new();

        public AccountGroup(string name)
        {
            Name = name;
        }

        public AccountGroup() { }
    }
}
