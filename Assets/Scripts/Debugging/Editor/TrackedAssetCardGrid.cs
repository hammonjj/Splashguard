using System;
using UnityEditor;
using UnityEngine;

namespace BitBox.Toymageddon.Debugging.Editor
{
    internal readonly struct TrackedAssetCardResult
    {
        public static readonly TrackedAssetCardResult None = new TrackedAssetCardResult(false, false);

        public TrackedAssetCardResult(bool openClicked, bool removeClicked)
        {
            OpenClicked = openClicked;
            RemoveClicked = removeClicked;
        }

        public bool OpenClicked { get; }

        public bool RemoveClicked { get; }
    }

    internal static class TrackedAssetCardGrid
    {
        public const float PreferredCardWidth = 272f;
        public const float CardHeight = 38f;
        public const float CardSpacing = 8f;

        private const float CardPadding = 7f;
        private const float CardContentGap = 6f;
        private const float ActionButtonGap = 4f;
        private const float ButtonSize = 20f;
        private const float IconSize = 18f;
        private const float ActionAreaWidth = (ButtonSize * 2f) + ActionButtonGap;

        private static GUIStyle _primaryLabelStyle;

        public static bool DrawFixedCardGrid(int itemCount, float availableWidth, Func<int, Rect, bool> drawCard)
        {
            if (itemCount <= 0)
            {
                return false;
            }

            var cardWidth = Mathf.Min(PreferredCardWidth, availableWidth);
            var columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + CardSpacing) / (cardWidth + CardSpacing)));
            var index = 0;

            while (index < itemCount)
            {
                EditorGUILayout.BeginHorizontal();

                for (var column = 0; column < columns && index < itemCount; column++, index++)
                {
                    var cardRect = GUILayoutUtility.GetRect(
                        cardWidth,
                        CardHeight,
                        GUILayout.Width(cardWidth),
                        GUILayout.Height(CardHeight));

                    if (drawCard(index, cardRect))
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        return true;
                    }

                    if (column < columns - 1 && index < itemCount - 1)
                    {
                        GUILayout.Space(CardSpacing);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (index < itemCount)
                {
                    EditorGUILayout.Space(CardSpacing);
                }
            }

            return false;
        }

        public static bool DrawFixedCardGrid(int itemCount, float availableWidth, int fixedColumnCount, Func<int, Rect, bool> drawCard)
        {
            if (itemCount <= 0)
            {
                return false;
            }

            var columns = Mathf.Max(1, fixedColumnCount);
            var totalSpacing = CardSpacing * (columns - 1);
            var cardWidth = Mathf.Max(1f, (availableWidth - totalSpacing) / columns);
            cardWidth = Mathf.Min(PreferredCardWidth, cardWidth);
            var index = 0;

            while (index < itemCount)
            {
                EditorGUILayout.BeginHorizontal();

                for (var column = 0; column < columns && index < itemCount; column++, index++)
                {
                    var cardRect = GUILayoutUtility.GetRect(
                        cardWidth,
                        CardHeight,
                        GUILayout.Width(cardWidth),
                        GUILayout.Height(CardHeight));

                    if (drawCard(index, cardRect))
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        return true;
                    }

                    if (column < columns - 1 && index < itemCount - 1)
                    {
                        GUILayout.Space(CardSpacing);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (index < itemCount)
                {
                    EditorGUILayout.Space(CardSpacing);
                }
            }

            return false;
        }

        public static float GetRequiredWidthForColumns(int columnCount)
        {
            var columns = Mathf.Max(1, columnCount);
            return (PreferredCardWidth * columns) + (CardSpacing * (columns - 1));
        }

        public static TrackedAssetCardResult DrawTrackedAssetCard(
            Rect rect,
            UnityEngine.Object asset,
            string primaryText,
            string tooltip,
            bool canOpen,
            string openTooltip,
            string removeTooltip)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var contentRect = new Rect(
                rect.x + CardPadding,
                rect.y + CardPadding,
                rect.width - (CardPadding * 2f) - ActionAreaWidth - CardContentGap,
                rect.height - (CardPadding * 2f));
            var actionRect = new Rect(
                rect.xMax - CardPadding - ActionAreaWidth,
                rect.y + CardPadding,
                ActionAreaWidth,
                rect.height - (CardPadding * 2f));

            var iconRect = new Rect(contentRect.x, contentRect.y + 2f, IconSize, IconSize);
            iconRect.y = contentRect.y + ((contentRect.height - IconSize) * 0.5f);
            var textRect = new Rect(
                iconRect.xMax + CardContentGap,
                contentRect.y,
                Mathf.Max(1f, contentRect.width - IconSize - CardContentGap),
                contentRect.height);
            var primaryRect = new Rect(textRect.x, textRect.y, textRect.width, textRect.height);
            var buttonY = actionRect.y + ((actionRect.height - ButtonSize) * 0.5f);
            var openRect = new Rect(actionRect.x, buttonY, ButtonSize, ButtonSize);
            var removeRect = new Rect(openRect.xMax + ActionButtonGap, buttonY, ButtonSize, ButtonSize);

            DrawAssetIcon(iconRect, asset, tooltip);
            DrawLabel(primaryRect, primaryText, tooltip, GetPrimaryLabelStyle());
            EditorUtils.HandleAssetDrag(contentRect, asset, primaryText);

            var openClicked = false;
            using (new EditorGUI.DisabledScope(!canOpen))
            {
                openClicked = GUI.Button(openRect, GetOpenIconContent(openTooltip));
            }

            var removeClicked = GUI.Button(removeRect, GetRemoveIconContent(removeTooltip));
            return new TrackedAssetCardResult(openClicked, removeClicked);
        }

        private static void DrawAssetIcon(Rect rect, UnityEngine.Object asset, string tooltip)
        {
            var iconContent = asset != null
                ? EditorGUIUtility.ObjectContent(asset, asset.GetType())
                : EditorGUIUtility.IconContent("console.warnicon");

            if (iconContent == null)
            {
                iconContent = GUIContent.none;
            }

            var image = iconContent.image;
            if (image == null)
            {
                return;
            }

            var iconContentWithTooltip = new GUIContent(image, tooltip);
            GUI.Label(rect, iconContentWithTooltip);
        }

        private static void DrawLabel(Rect rect, string text, string tooltip, GUIStyle style)
        {
            var truncatedText = TruncateToWidth(text, style, rect.width);
            GUI.Label(rect, new GUIContent(truncatedText, tooltip), style);
        }

        private static string TruncateToWidth(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            {
                return string.Empty;
            }

            if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            for (var length = text.Length - 1; length > 0; length--)
            {
                var candidate = text.Substring(0, length) + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private static GUIStyle GetPrimaryLabelStyle()
        {
            if (_primaryLabelStyle == null)
            {
                _primaryLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    fontStyle = FontStyle.Bold
                };
            }

            return _primaryLabelStyle;
        }

        private static GUIContent GetOpenIconContent(string tooltip)
        {
            var icon = EditorGUIUtility.IconContent("FolderOpened On Icon");
            if (icon == null || icon.image == null)
            {
                icon = new GUIContent("O");
            }

            icon.tooltip = tooltip;
            return icon;
        }

        private static GUIContent GetRemoveIconContent(string tooltip)
        {
            var icon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (icon == null || icon.image == null)
            {
                icon = new GUIContent("x");
            }

            icon.tooltip = tooltip;
            return icon;
        }
    }
}
