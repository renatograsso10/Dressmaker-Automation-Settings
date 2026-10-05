using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DressmakerAutomation.SettingsUI
{
    public sealed class WindowFrame : MonoBehaviour
    {
        private const float Width = 690f;
        private const float Height = 780f;
        private const float Padding = 14f;
        private const float DefaultScale = 0.8f;
        private RectTransform window;
        private RectTransform bounds;
        private Vector2 lastBounds;
        private Vector2 pointerStart;
        private Vector2 centerStart;
        private Vector2 topLeftStart;
        private float scaleStart;
        private bool resizing;

        [Serializable]
        private sealed class Layout
        {
            public float scale = DefaultScale;
            public float x;
            public float y;
        }

        internal void Configure(RectTransform panel, RectTransform parent, GameObject title, GameObject corner)
        {
            window = panel;
            bounds = parent;
            title.AddComponent<WindowHandle>().Configure(this, false);
            corner.AddComponent<WindowHandle>().Configure(this, true);
            Canvas.ForceUpdateCanvases();
            Restore();
        }

        private float MaximumScale => Mathf.Min(1.1f, (bounds.rect.width - Padding * 2) / Width, (bounds.rect.height - Padding * 2) / Height);
        private float LimitScale(float value) => Mathf.Clamp(value, Mathf.Min(0.65f, MaximumScale), MaximumScale);
        private Vector2 Travel => new Vector2(Mathf.Max(0, (bounds.rect.width - Width * window.localScale.x) / 2 - Padding), Mathf.Max(0, (bounds.rect.height - Height * window.localScale.x) / 2 - Padding));

        internal void Begin(PointerEventData pointer, bool resize)
        {
            if (pointer.button != PointerEventData.InputButton.Left) return;
            resizing = resize;
            pointerStart = Point(pointer);
            centerStart = window.anchoredPosition;
            scaleStart = window.localScale.x;
            topLeftStart = centerStart + new Vector2(-Width, Height) * (scaleStart / 2);
        }

        internal void Drag(PointerEventData pointer)
        {
            if (pointer.button != PointerEventData.InputButton.Left) return;
            var delta = Point(pointer) - pointerStart;
            if (resizing)
            {
                float dx = delta.x / Width;
                float dy = -delta.y / Height;
                float scale = LimitScale(scaleStart + (Mathf.Abs(dx) >= Mathf.Abs(dy) ? dx : dy));
                // Keep the upper-left corner fixed while scaling the entire menu proportionally.
                float available = Mathf.Min((bounds.rect.xMax - Padding - topLeftStart.x) / Width, (topLeftStart.y - bounds.rect.yMin - Padding) / Height);
                scale = Mathf.Min(scale, available);
                window.localScale = Vector3.one * scale;
                window.anchoredPosition = topLeftStart + new Vector2(Width, -Height) * (scale / 2);
            }
            else window.anchoredPosition = centerStart + delta;
            Constrain();
        }

        private Vector2 Point(PointerEventData pointer)
        {
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, pointer.position, pointer.pressEventCamera, out point);
            return point;
        }

        private void Constrain()
        {
            window.localScale = Vector3.one * LimitScale(window.localScale.x);
            var travel = Travel;
            var center = window.anchoredPosition;
            window.anchoredPosition = new Vector2(Mathf.Clamp(center.x, -travel.x, travel.x), Mathf.Clamp(center.y, -travel.y, travel.y));
            lastBounds = bounds.rect.size;
        }

        internal void ResetLayout()
        {
            window.localScale = Vector3.one * LimitScale(DefaultScale);
            window.anchoredPosition = Vector2.zero;
            Constrain();
            SaveLayout();
        }

        internal void SaveLayout()
        {
            if (window == null || bounds == null) return;
            try
            {
                Constrain();
                var travel = Travel;
                var layout = new Layout { scale = window.localScale.x, x = travel.x > 0 ? window.anchoredPosition.x / travel.x : 0, y = travel.y > 0 ? window.anchoredPosition.y / travel.y : 0 };
                string path = Path.Combine(Application.persistentDataPath, "DressmakerAutomation.UI.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(layout));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".backup", true);
                else File.Move(temporary, path);
            }
            catch (Exception error) { Debug.LogWarning("[DressmakerAutomation.UI] Could not save window layout: " + error.Message); }
        }

        private void Restore()
        {
            var layout = new Layout();
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "DressmakerAutomation.UI.json");
                if (File.Exists(path)) layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path)) ?? layout;
                if (float.IsNaN(layout.scale) || float.IsInfinity(layout.scale) || float.IsNaN(layout.x) || float.IsInfinity(layout.x) || float.IsNaN(layout.y) || float.IsInfinity(layout.y)) layout = new Layout();
            }
            catch (Exception error) { Debug.LogWarning("[DressmakerAutomation.UI] Could not read window layout: " + error.Message); }
            window.localScale = Vector3.one * LimitScale(layout.scale);
            var travel = Travel;
            window.anchoredPosition = new Vector2(Mathf.Clamp(layout.x, -1, 1) * travel.x, Mathf.Clamp(layout.y, -1, 1) * travel.y);
            Constrain();
        }

        private void LateUpdate()
        {
            if (window != null && bounds != null && bounds.rect.size != lastBounds) Constrain();
        }
    }

    public sealed class WindowHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private WindowFrame frame;
        private bool resize;
        internal void Configure(WindowFrame owner, bool isResize) { frame = owner; resize = isResize; }
        public void OnBeginDrag(PointerEventData pointer) { frame.Begin(pointer, resize); }
        public void OnDrag(PointerEventData pointer) { frame.Drag(pointer); }
        public void OnEndDrag(PointerEventData pointer) { frame.SaveLayout(); }
        public void OnPointerClick(PointerEventData pointer)
        {
            if (!resize && pointer.button == PointerEventData.InputButton.Left && pointer.clickCount == 2) frame.ResetLayout();
        }
    }
}
