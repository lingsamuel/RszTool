namespace RszTool.App.ViewModels
{
    public abstract class BaseTreeItemViewModel(string name)
    {
        public string Name { get; set; } = name;
        public abstract IEnumerable<object>? Items { get; }
    }


    public class TreeItemViewModel(string name, IEnumerable<object> items) : BaseTreeItemViewModel(name)
    {
        public override IEnumerable<object>? Items { get; } = items;
    }


    public class TreeItemDelegate(string name, Func<IEnumerable<object>>? itemsFunc) : BaseTreeItemViewModel(name)
    {
        public Func<IEnumerable<object>>? ItemsFunc { get; set; } = itemsFunc;
        public override IEnumerable<object>? Items => ItemsFunc?.Invoke() ?? null;
    }
}