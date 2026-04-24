using System;
using System.Reflection;
using BitBox.Library;
using BitBox.Library.Eventing;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Eventing.GlobalEvents;
using BitBox.Library.UI.Toolkit;
using BitBox.Toymageddon.Settings;
using BitBox.Toymageddon.UserInterface;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class FrontendUiDecompositionTests
    {
        private const string PanelSettingsResourcePath = "Frontend/FrontendRuntimePanelSettings";
        private const float ComparisonTolerance = 0.0001f;

        [Test]
        public void SettingsOverlayController_DownNavigationTargetsEachPageEntryControl()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateSettingsController(fixture.Runtime);

            SettingsNavigationResult generalResult = controller.EvaluateNavigationForTesting(
                NavigationMoveEvent.Direction.Down,
                fixture.Runtime.SettingsScreen.GeneralTabButton);
            SettingsNavigationResult gameplayResult = controller.EvaluateNavigationForTesting(
                NavigationMoveEvent.Direction.Down,
                fixture.Runtime.SettingsScreen.GameplayTabButton);
            SettingsNavigationResult soundResult = controller.EvaluateNavigationForTesting(
                NavigationMoveEvent.Direction.Down,
                fixture.Runtime.SettingsScreen.SoundTabButton);

            NUnitAssert.IsTrue(generalResult.Consumed);
            NUnitAssert.AreEqual(SettingsTab.General, generalResult.TargetTab);
            NUnitAssert.AreSame(fixture.Runtime.SettingsScreen.UiScaleSlider, generalResult.FocusTarget);

            NUnitAssert.IsTrue(gameplayResult.Consumed);
            NUnitAssert.AreEqual(SettingsTab.Gameplay, gameplayResult.TargetTab);
            NUnitAssert.AreSame(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle, gameplayResult.FocusTarget);

            NUnitAssert.IsTrue(soundResult.Consumed);
            NUnitAssert.AreEqual(SettingsTab.Sound, soundResult.TargetTab);
            NUnitAssert.AreSame(fixture.Runtime.SettingsScreen.MasterVolumeSlider, soundResult.FocusTarget);
        }

        [Test]
        public void SettingsOverlayController_UpFromFirstControlReturnsToMatchingTab()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateSettingsController(fixture.Runtime);

            SettingsNavigationResult gameplayResult = controller.EvaluateNavigationForTesting(
                NavigationMoveEvent.Direction.Up,
                fixture.Runtime.SettingsScreen.InvertVerticalAimToggle);
            SettingsNavigationResult soundResult = controller.EvaluateNavigationForTesting(
                NavigationMoveEvent.Direction.Up,
                fixture.Runtime.SettingsScreen.MasterVolumeSlider);

            NUnitAssert.IsTrue(gameplayResult.Consumed);
            NUnitAssert.AreEqual(SettingsTab.Gameplay, gameplayResult.TargetTab);
            NUnitAssert.AreSame(fixture.Runtime.SettingsScreen.GameplayTabButton, gameplayResult.FocusTarget);

            NUnitAssert.IsTrue(soundResult.Consumed);
            NUnitAssert.AreEqual(SettingsTab.Sound, soundResult.TargetTab);
            NUnitAssert.AreSame(fixture.Runtime.SettingsScreen.SoundTabButton, soundResult.FocusTarget);
        }

        [Test]
        public void SettingsOverlayController_RightNavigationUpdatesActiveTab()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateSettingsController(fixture.Runtime);

            bool movedToGameplay = controller.ApplyNavigationMoveForTesting(
                NavigationMoveEvent.Direction.Right,
                fixture.Runtime.SettingsScreen.GeneralTabButton);
            bool movedToSound = controller.ApplyNavigationMoveForTesting(
                NavigationMoveEvent.Direction.Right,
                fixture.Runtime.SettingsScreen.GameplayTabButton);

            NUnitAssert.IsTrue(movedToGameplay);
            NUnitAssert.AreEqual(SettingsTab.Gameplay, controller.ActiveTab);

            NUnitAssert.IsTrue(movedToSound);
            NUnitAssert.AreEqual(SettingsTab.Sound, controller.ActiveTab);
        }

        [Test]
        public void SettingsOverlayController_FocusStylingMarksControlAndRow()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateSettingsController(fixture.Runtime);
            VisualElement row = FindContainingSettingsRow(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle);

            controller.ApplyFocusStylingForTesting(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle, isFocused: true);

            NUnitAssert.IsTrue(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle.ClassListContains("settings-focusable--focused"));
            NUnitAssert.IsTrue(row.ClassListContains("settings-row--focused"));

            controller.ApplyFocusStylingForTesting(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle, isFocused: false);

            NUnitAssert.IsFalse(fixture.Runtime.SettingsScreen.InvertVerticalAimToggle.ClassListContains("settings-focusable--focused"));
            NUnitAssert.IsFalse(row.ClassListContains("settings-row--focused"));
        }

        [Test]
        public void TitleFlowController_ShowJoinPromptActivatesJoinPromptScreen()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateTitleController(fixture.Runtime);

            controller.ShowTitle();
            controller.ShowJoinPromptForTesting(4f);

            NUnitAssert.IsTrue(controller.IsAwaitingJoinPromptInput);
            NUnitAssert.AreEqual(FrontendUiScreenIds.JoinPrompt, fixture.Runtime.ScreenHost.ActiveBaseScreenId);
        }

        [Test]
        public void TitleFlowController_DebounceRequiresThresholdBeforeAccept()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateTitleController(fixture.Runtime);

            controller.ShowJoinPromptForTesting(10f);

            NUnitAssert.IsFalse(controller.CanAcceptJoinInputForTesting(10.19f));
            NUnitAssert.IsTrue(controller.CanAcceptJoinInputForTesting(10.2f));
        }

        [Test]
        public void TitleFlowController_CancelReturnsToTitle()
        {
            using var fixture = FrontendTestFixture.Create();
            var controller = CreateTitleController(fixture.Runtime);

            controller.ShowJoinPromptForTesting(2f);
            bool handled = controller.HandleCancel();

            NUnitAssert.IsTrue(handled);
            NUnitAssert.IsFalse(controller.IsAwaitingJoinPromptInput);
            NUnitAssert.AreEqual(FrontendUiScreenIds.Title, fixture.Runtime.ScreenHost.ActiveBaseScreenId);
        }

        [Test]
        public void PauseOverlayController_RestoreFromSettingsShowsPauseOverlayAgain()
        {
            using var fixture = FrontendTestFixture.Create();
            var bindings = new UiBindingScope();
            var controller = new PauseOverlayController(
                fixture.Runtime.PauseScreen,
                fixture.Runtime.ScreenHost,
                bindings,
                fixture.Host.AddComponent<PauseControllerTestHost>(),
                fixture.Host,
                () => Array.Empty<PlayerInput>(),
                () => { },
                _ => { },
                _ => { },
                _ => { });
            controller.Initialize();
            controller.SetOwningPlayerForTesting(0);

            controller.PrepareForSettings();
            NUnitAssert.AreEqual(DisplayStyle.None, fixture.Runtime.PauseScreen.Card.style.display.value);

            bool restored = controller.RestoreFromSettings();

            NUnitAssert.IsTrue(restored);
            NUnitAssert.IsTrue(fixture.Runtime.ScreenHost.IsOverlayVisible(FrontendUiScreenIds.Pause));
            NUnitAssert.AreEqual(DisplayStyle.Flex, fixture.Runtime.PauseScreen.Card.style.display.value);

            controller.Dispose();
            bindings.Dispose();
        }

        [Test]
        public void FrontendUiController_MacroSceneRoutingAndSettingsSyncStillWork()
        {
            var host = new GameObject("FrontendUiControllerIntegrationTest");
            try
            {
                var controller = host.AddComponent<FrontendUiController>();

                InvokePrivate(controller, "BuildUiIfNeeded");
                InvokePrivate(controller, "OnMacroSceneLoaded", new MacroSceneLoadedEvent(MacroSceneType.TitleMenu));

                var runtime = GetPrivateField<FrontendUiRuntime>(controller, "_runtime");
                NUnitAssert.AreEqual(FrontendUiScreenIds.Title, runtime.ScreenHost.ActiveBaseScreenId);

                var updatedSettings = new GameSettingsSnapshot(
                    masterVolume01: 1f,
                    musicVolume01: 0.8f,
                    sfxVolume01: 0.6f,
                    languageId: "en",
                    uiScale: 1.25f,
                    invertVerticalAim: true);
                InvokePrivate(controller, "OnGameSettingsChanged", updatedSettings);

                NUnitAssert.AreEqual(UiScaleSettings.FormatPercentLabel(1.25f), runtime.SettingsScreen.UiScaleValueLabel.text);
                float appliedScale = runtime.FrontendRootElement.style.scale.value.value.x;
                NUnitAssert.That(appliedScale, Is.EqualTo(1.25f).Within(ComparisonTolerance));

                InvokePrivate(controller, "OnMacroSceneLoaded", new MacroSceneLoadedEvent(MacroSceneType.HubWorld));
                NUnitAssert.IsNull(runtime.ScreenHost.ActiveBaseScreenId);

                UnityEngine.Object.DestroyImmediate(controller);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FrontendUiController_JoinPromptAcceptanceRequestsCharacterSelection()
        {
            var busHost = new GameObject("FrontendUiControllerBus");
            var host = new GameObject("FrontendUiControllerJoinPromptTest");
            LoadMacroSceneEvent publishedEvent = null;
            StaticData.PendingInitialJoinRequest = null;
            GlobalStaticData.GlobalMessageBus = busHost.AddComponent<MessageBus>();

            try
            {
                GlobalStaticData.GlobalMessageBus.Subscribe<LoadMacroSceneEvent>(@event => publishedEvent = @event);

                var controller = host.AddComponent<FrontendUiController>();
                InvokePrivate(controller, "BuildUiIfNeeded");

                var pendingJoinRequest = new PendingPlayerJoinRequest
                {
                    ControlScheme = "Gamepad",
                    SourceControlPath = "<Gamepad>/buttonSouth"
                };

                InvokePrivate(controller, "OnJoinPromptAccepted", pendingJoinRequest);

                NUnitAssert.AreSame(pendingJoinRequest, StaticData.PendingInitialJoinRequest);
                NUnitAssert.IsNotNull(publishedEvent);
                NUnitAssert.AreEqual(MacroSceneType.CharacterSelection, publishedEvent.SceneType);

                UnityEngine.Object.DestroyImmediate(controller);
            }
            finally
            {
                StaticData.PendingInitialJoinRequest = null;
                GlobalStaticData.GlobalMessageBus = null;
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(busHost);
            }
        }

        [Test]
        public void CharacterSelectionUiRuntimeBuilder_CreatesCharacterSelectionHierarchyUnderViewportRoot()
        {
            GameObject playerRoot = new("PlayerRoot");
            try
            {
                GameObject uiCanvas = new("UiCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                uiCanvas.transform.SetParent(playerRoot.transform, false);

                GameObject viewportRoot = new("ViewportRoot", typeof(RectTransform));
                viewportRoot.transform.SetParent(uiCanvas.transform, false);

                GameObject container = CharacterSelectionUiRuntimeBuilder.EnsureBuilt(playerRoot.transform);

                NUnitAssert.IsNotNull(container);
                NUnitAssert.AreEqual("CharacterSelectionRoot", container.name);
                NUnitAssert.IsFalse(container.activeSelf);
                var overlayImage = container.GetComponent<UnityEngine.UI.Image>();
                NUnitAssert.IsNotNull(overlayImage);
                NUnitAssert.AreEqual(0f, overlayImage.color.a);
                NUnitAssert.IsNotNull(container.transform.Find("Panel/CardSurface/Title"));
                NUnitAssert.IsNotNull(container.transform.Find("Panel/CardSurface/Subtitle"));
                NUnitAssert.IsNotNull(container.transform.Find("Panel/CardSurface/Ready"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerRoot);
            }
        }

        [Test]
        public void CharacterSelectionUiRuntimeBuilder_ReusesExistingHierarchy()
        {
            GameObject playerRoot = new("PlayerRoot");
            try
            {
                GameObject uiCanvas = new("UiCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                uiCanvas.transform.SetParent(playerRoot.transform, false);

                GameObject viewportRoot = new("ViewportRoot", typeof(RectTransform));
                viewportRoot.transform.SetParent(uiCanvas.transform, false);

                GameObject first = CharacterSelectionUiRuntimeBuilder.EnsureBuilt(playerRoot.transform);
                GameObject second = CharacterSelectionUiRuntimeBuilder.EnsureBuilt(playerRoot.transform);

                NUnitAssert.AreSame(first, second);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerRoot);
            }
        }

        private static SettingsOverlayController CreateSettingsController(FrontendUiRuntime runtime)
        {
            var bindings = new UiBindingScope();
            var snapshot = new GameSettingsSnapshot(
                masterVolume01: 0.8f,
                musicVolume01: 0.7f,
                sfxVolume01: 0.6f,
                languageId: "en",
                uiScale: 1.1f,
                invertVerticalAim: false);

            var controller = new SettingsOverlayController(
                runtime.SettingsScreen,
                runtime.ScreenHost,
                bindings,
                () => null,
                () => snapshot,
                _ => { },
                () => { });
            controller.Initialize();
            return controller;
        }

        private static TitleFlowController CreateTitleController(FrontendUiRuntime runtime)
        {
            var bindings = new UiBindingScope();
            var controller = new TitleFlowController(
                runtime,
                bindings,
                () => { },
                _ => { },
                _ => { });
            controller.Initialize();
            return controller;
        }

        private static VisualElement FindContainingSettingsRow(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("settings-row"))
                {
                    return element;
                }

                element = element.parent;
            }

            return null;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(method, $"Expected method '{methodName}' on '{target.GetType().Name}'.");
            method.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            NUnitAssert.IsNotNull(field, $"Expected field '{fieldName}' on '{target.GetType().Name}'.");
            return (T)field.GetValue(target);
        }

        private sealed class FrontendTestFixture : IDisposable
        {
            private FrontendTestFixture(GameObject host, FrontendUiRuntime runtime)
            {
                Host = host;
                Runtime = runtime;
            }

            public GameObject Host { get; }
            public FrontendUiRuntime Runtime { get; }

            public static FrontendTestFixture Create()
            {
                var host = new GameObject("FrontendTestFixture");
                var uiDocument = host.AddComponent<UIDocument>();
                uiDocument.panelSettings = Resources.Load<PanelSettings>(PanelSettingsResourcePath);
                FrontendUiRuntime runtime = new FrontendUiBuilder().Build(uiDocument);
                return new FrontendTestFixture(host, runtime);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Host);
            }
        }

        private sealed class PauseControllerTestHost : MonoBehaviour
        {
        }
    }
}
