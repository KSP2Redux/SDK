using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;

namespace UitkForKsp2.Controls
{
    public enum AppShellTitleSpacing
    {
        Normal,
        Wide,
        Compact
    }

    [UxmlElement]
    public partial class AppShell : VisualElement
    {
        private const string UppercaseLocalizationClass = "uppercase";
        private const string DefaultDashes = "----------------------------------------------------------------------------------------------------------------------------------------------------------------/";
        private const string WideTitleClass = "oab-window-header-title--wide";
        private const string CompactTitleClass = "oab-window-header-title--compact";

        private readonly VisualElement _header;
        private readonly VisualElement _icon;
        private readonly Label _title;
        private readonly Label _dashes;
        private readonly VisualElement _closeContainer;
        private readonly Button _closeButton;
        private readonly VisualElement _content;

        private AsyncOperationHandle<UnityEngine.Object>? _iconLoadHandle;
        private string _titleText = string.Empty;
        private string _iconAddress = string.Empty;
        private Texture2D _iconTexture;
        private Sprite _iconSprite;
        private bool _uppercaseTitle = true;
        private bool _showCloseButton = true;
        private AppShellTitleSpacing _titleSpacing = AppShellTitleSpacing.Normal;

        public override VisualElement contentContainer => _content;

        public AppShell()
        {
            AddToClassList("root");
            AddToClassList("oab-window-root");

            _header = new VisualElement { name = "header" };
            _header.AddToClassList("oab-window-header");

            _icon = new VisualElement { name = "header-icon" };
            _icon.AddToClassList("oab-window-header-icon");

            _title = new Label { name = "header-title" };
            _title.AddToClassList("oab-window-header-title");

            _dashes = new Label(DefaultDashes) { name = "header-dashes" };
            _dashes.AddToClassList("oab-window-header-dashes");

            _closeContainer = new VisualElement { name = "header-close-container" };
            _closeContainer.AddToClassList("oab-window-close-container");

            _closeButton = new Button { name = "header-close-button", text = "x" };
            _closeButton.AddToClassList("oab-close-button");
            _closeButton.AddToClassList("ui-sound-close");
            _closeButton.clicked += OnCloseButtonClicked;

            _content = new VisualElement { name = "content" };
            _content.AddToClassList("oab-window-content");

            BuildVisualTree();
            UpdateTitle();
            UpdateTitleClasses();
            UpdateIcon();

            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseIconHandle());
        }

        public event Action CloseClicked;

        public Button CloseButton => _closeButton;

        [CreateProperty]
        [UxmlAttribute("title")]
        public string Title
        {
            get => _titleText;
            set
            {
                _titleText = value ?? string.Empty;
                UpdateTitle();
            }
        }

        [CreateProperty]
        [UxmlAttribute("uppercase-title")]
        public bool UppercaseTitle
        {
            get => _uppercaseTitle;
            set
            {
                _uppercaseTitle = value;
                UpdateTitle();
                UpdateTitleClasses();
            }
        }

        [CreateProperty]
        [UxmlAttribute("title-spacing")]
        public AppShellTitleSpacing TitleSpacing
        {
            get => _titleSpacing;
            set
            {
                _titleSpacing = value;
                UpdateTitleClasses();
            }
        }

        [CreateProperty]
        [UxmlAttribute("icon-address")]
        public string IconAddress
        {
            get => _iconAddress;
            set
            {
                _iconAddress = value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_iconAddress))
                {
                    _iconTexture = null;
                    _iconSprite = null;
                }

                UpdateIcon();
            }
        }

        [CreateProperty]
        [UxmlAttribute("icon-texture")]
        public Texture2D IconTexture
        {
            get => _iconTexture;
            set
            {
                _iconTexture = value;
                if (_iconTexture != null)
                {
                    _iconAddress = string.Empty;
                    _iconSprite = null;
                }

                UpdateIcon();
            }
        }

        [CreateProperty]
        [UxmlAttribute("icon-sprite")]
        public Sprite IconSprite
        {
            get => _iconSprite;
            set
            {
                _iconSprite = value;
                if (_iconSprite != null)
                {
                    _iconAddress = string.Empty;
                    _iconTexture = null;
                }

                UpdateIcon();
            }
        }

        [CreateProperty]
        [UxmlAttribute("dashes")]
        public string Dashes
        {
            get => _dashes.text;
            set => _dashes.text = string.IsNullOrEmpty(value) ? DefaultDashes : value;
        }

        [CreateProperty]
        [UxmlAttribute("show-close-button")]
        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set
            {
                _showCloseButton = value;
                _closeContainer.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        [UxmlAttribute("close-container-name")]
        public string CloseContainerName
        {
            get => _closeContainer.name;
            set => _closeContainer.name = string.IsNullOrWhiteSpace(value) ? "header-close-container" : value;
        }

        [UxmlAttribute("close-button-name")]
        public string CloseButtonName
        {
            get => _closeButton.name;
            set => _closeButton.name = string.IsNullOrWhiteSpace(value) ? "header-close-button" : value;
        }

        [UxmlAttribute("title-name")]
        public string TitleName
        {
            get => _title.name;
            set => _title.name = string.IsNullOrWhiteSpace(value) ? "header-title" : value;
        }

        [UxmlAttribute("icon-name")]
        public string IconName
        {
            get => _icon.name;
            set => _icon.name = string.IsNullOrWhiteSpace(value) ? "header-icon" : value;
        }

        [UxmlAttribute("dashes-name")]
        public string DashesName
        {
            get => _dashes.name;
            set => _dashes.name = string.IsNullOrWhiteSpace(value) ? "header-dashes" : value;
        }

        private void BuildVisualTree()
        {
            _closeContainer.Clear();
            _header.Clear();

            _closeContainer.Add(_closeButton);
            _header.Add(_icon);
            _header.Add(_title);
            _header.Add(_dashes);
            _header.Add(_closeContainer);

            hierarchy.Add(_header);
            hierarchy.Add(_content);
        }

        private void UpdateTitle()
        {
            _title.text = _uppercaseTitle && !string.IsNullOrEmpty(_titleText) && _titleText[0] != '#'
                ? _titleText.ToUpperInvariant()
                : _titleText;
        }

        private void UpdateTitleClasses()
        {
            _title.EnableInClassList(UppercaseLocalizationClass, _uppercaseTitle);
            _title.EnableInClassList(WideTitleClass, _titleSpacing == AppShellTitleSpacing.Wide);
            _title.EnableInClassList(CompactTitleClass, _titleSpacing == AppShellTitleSpacing.Compact);
        }

        private void UpdateIcon()
        {
            ReleaseIconHandle();
            _icon.style.display = DisplayStyle.None;
            _icon.style.backgroundImage = StyleKeyword.Null;

            if (_iconTexture != null)
            {
                SetIconBackground(_iconTexture);
                return;
            }

            if (_iconSprite != null)
            {
                SetIconBackground(_iconSprite);
                return;
            }

            if (string.IsNullOrWhiteSpace(_iconAddress))
            {
                return;
            }

            _iconLoadHandle = Addressables.LoadAssetAsync<UnityEngine.Object>(_iconAddress);
            _iconLoadHandle.Value.Completed += OnIconLoaded;
        }

        private void OnIconLoaded(AsyncOperationHandle<UnityEngine.Object> handle)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"Failed to load AppShell icon from address '{_iconAddress}': {handle.OperationException}");
                return;
            }

            switch (handle.Result)
            {
                case Texture2D texture:
                    SetIconBackground(texture);
                    break;
                case Sprite sprite:
                    SetIconBackground(sprite);
                    break;
                default:
                    Debug.LogError(
                        $"AppShell icon address '{_iconAddress}' resolved to unsupported asset type '{handle.Result.GetType().Name}'"
                    );
                    break;
            }
        }

        private void SetIconBackground(Texture2D texture)
        {
            _icon.style.backgroundImage = new StyleBackground(texture);
            _icon.style.display = DisplayStyle.Flex;
        }

        private void SetIconBackground(Sprite sprite)
        {
            _icon.style.backgroundImage = new StyleBackground(sprite);
            _icon.style.display = DisplayStyle.Flex;
        }

        private void ReleaseIconHandle()
        {
            if (_iconLoadHandle is not { } handle)
            {
                return;
            }

            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            _iconLoadHandle = null;
        }

        private void OnCloseButtonClicked()
        {
            CloseClicked?.Invoke();
        }
    }
}
