using UnityEngine;

namespace AEM.DigitalTwin
{
    [ExecuteAlways]
    public class FactoryDashboardLayout : MonoBehaviour
    {
        public RectTransform container;
        private void LateUpdate()
        {
            if (container == null) return;
            float scale = Mathf.Min(container.rect.width / 480f, container.rect.height / 900f);
            transform.localScale = Vector3.one * scale;
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2((container.rect.width - 480f * scale) * .5f, -(container.rect.height - 900f * scale) * .5f);
        }
    }
}
