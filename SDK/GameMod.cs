using System;
using UnityEngine;

namespace FrontierTale.Modding
{
    /// <summary>SDK v1 入口。每次 Play 为一个启用的包创建一个实例，关闭时回收。</summary>
    public abstract class GameMod
    {
        public abstract void Load(ModContext context);
        public virtual void OnFrame() { }
        public virtual void OnViewBound(ModView view) { }
        public virtual void Unload() { }
    }

    /// <summary>每个模组拥有独立的窗口命名空间与资源目录。</summary>
    public abstract class ModContext
    {
        public abstract string Folder { get; }
        public abstract RectTransform OpenWindow(string name);
        public abstract void CloseWindow(string name);
        // 为自行发现的原版 UI 节点创建子节点，由模组掌握布局，卸载时自动回收。
        public abstract RectTransform Attach(RectTransform parent, string name);
    }

    /// <summary>一次 UI 数据绑定的有效期。切换物品、回收或关闭节点时结束。</summary>
    public sealed class ModView
    {
        public readonly Component View;
        public readonly object Model;
        private readonly Func<bool> valid;
        private readonly Func<RectTransform, string, RectTransform> mount;
        private readonly Action redraw;
        public bool IsCurrent => valid();

        public ModView(Component view, object model, Func<bool> valid,
            Func<RectTransform, string, RectTransform> mount, Action redraw)
        {
            View = view;
            Model = model;
            this.valid = valid;
            this.mount = mount;
            this.redraw = redraw;
        }

        // 在本次绑定的任意后代节点下创建，自上一次绑定留下的 UI 由宿主清理。
        public RectTransform Attach(RectTransform parent, string name)
        {
            if (!IsCurrent || !parent.IsChildOf(View.transform)) throw new InvalidOperationException("Invalid view binding");
            return mount(parent, name);
        }

        public void Refresh()
        {
            if (IsCurrent) redraw();
        }
    }
}
