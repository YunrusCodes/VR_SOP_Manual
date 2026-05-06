using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inspection.UI
{
    public sealed class LoadingOverlay : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] GameObject closeButtonRoot;
        [SerializeField] Button closeButton;

        public void Show(string text)
        {
            if (label != null) label.text = text;
            if (closeButtonRoot != null) closeButtonRoot.SetActive(false);
            gameObject.SetActive(true);
        }

        public void ShowMessage(string text)
        {
            if (label != null) label.text = text;
            if (closeButtonRoot != null) closeButtonRoot.SetActive(true);
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
