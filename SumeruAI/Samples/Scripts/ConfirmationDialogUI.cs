using System;
using UnityEngine;
using UnityEngine.UI;

namespace SumeruAI.Samples
{
    public class ConfirmationDialogUI : MonoBehaviour
    {
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private GameObject canvasRoot;
        private Action onConfirm;
        private Action onCancel;

        public static ConfirmationDialogUI Create(int layer, int sortingOrder = 500)
        {
            GameObject canvasObject = new GameObject(
                "confirmation-dialog-canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.layer = layer;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            GameObject rootObject = new GameObject("confirmation-dialog", typeof(RectTransform), typeof(Image), typeof(ConfirmationDialogUI));
            rootObject.layer = layer;
            rootObject.transform.SetParent(canvasObject.transform, false);
            rootObject.transform.SetAsLastSibling();

            RectTransform rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image overlayImage = rootObject.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.68f);

            GameObject panelObject = new GameObject("panel", typeof(RectTransform), typeof(Image));
            panelObject.layer = layer;
            panelObject.transform.SetParent(rootRect, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520f, 150f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.16470589f, 0.16470589f, 0.16470589f, 0.98f);

            GameObject textObject = new GameObject("message", typeof(RectTransform), typeof(Text));
            textObject.layer = layer;
            textObject.transform.SetParent(panelRect, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -18f);
            textRect.sizeDelta = new Vector2(470f, 70f);

            Text text = textObject.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 24;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            Button confirm = CreateButton(
                panelRect,
                layer,
                "confirm-btn",
                "Confirm",
                new Vector2(-95f, -42f),
                new Color(0.78039217f, 0.25490198f, 0.25490198f, 1f),
                new Color(0.6862745f, 0.20392157f, 0.20392157f, 1f),
                new Color(0.8901961f, 0.37254903f, 0.37254903f, 1f));

            Button cancel = CreateButton(
                panelRect,
                layer,
                "cancel-btn",
                "Cancel",
                new Vector2(95f, -42f),
                new Color(0.4f, 0.4f, 0.4f, 1f),
                new Color(0.33f, 0.33f, 0.33f, 1f),
                new Color(0.55f, 0.55f, 0.55f, 1f));

            ConfirmationDialogUI dialog = rootObject.GetComponent<ConfirmationDialogUI>();
            dialog.canvasRoot = canvasObject;
            dialog.messageText = text;
            dialog.confirmButton = confirm;
            dialog.cancelButton = cancel;
            dialog.BindButtons();
            dialog.Hide();
            return dialog;
        }

        public void Show(
            string message,
            Action onConfirm,
            Action onCancel = null,
            string confirmButtonText = "Confirm",
            string cancelButtonText = "Cancel")
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            if (canvasRoot != null)
            {
                canvasRoot.SetActive(true);
            }

            SetButtonLabel(confirmButton, confirmButtonText);
            SetButtonLabel(cancelButton, cancelButtonText);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (canvasRoot != null)
            {
                Destroy(canvasRoot);
                canvasRoot = null;
                return;
            }

            Destroy(gameObject);
        }

        private void BindButtons()
        {
            BindButton(confirmButton, HandleConfirm);
            BindButton(cancelButton, HandleCancel);
        }

        private void HandleConfirm()
        {
            Hide();
            onConfirm?.Invoke();
        }

        private void HandleCancel()
        {
            Hide();
            onCancel?.Invoke();
        }

        private static Button CreateButton(
            RectTransform parent,
            int layer,
            string objectName,
            string labelText,
            Vector2 anchoredPosition,
            Color normalColor,
            Color highlightedColor,
            Color pressedColor)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.layer = layer;
            buttonObject.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(150f, 46f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = normalColor;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = pressedColor;
            colors.selectedColor = pressedColor;
            colors.disabledColor = new Color(0.2735849f, 0f, 0f, 0.5019608f);
            button.colors = colors;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObject.layer = layer;
            labelObject.transform.SetParent(buttonRect, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObject.GetComponent<Text>();
            label.text = labelText;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 22;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
            return button;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
