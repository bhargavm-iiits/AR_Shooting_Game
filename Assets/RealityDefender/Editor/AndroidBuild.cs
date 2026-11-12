using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.Management;

namespace RealityDefender.Editor
{
    /// <summary>Repeatable single-file, ARM64 Android delivery for the assignment.</summary>
    public static class AndroidBuild
    {
        public const string ScenePath = "Assets/RealityDefender/Scenes/RealityDefender.unity";
        public const string ApkPath = "Builds/Android/RealityDefender.apk";
        const string SettingsFolder = "Assets/RealityDefender/Settings";

        [MenuItem("Reality Defender/Android/Configure Android")]
        public static void Configure()
        {
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "Assignment";
            PlayerSettings.productName = "The Reality Defender";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.assignment.realitydefender");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.openGLRequireES31 = false;
            PlayerSettings.openGLRequireES31AEP = false;
            PlayerSettings.openGLRequireES32 = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.Android.autoRotationBehavior = AndroidAutoRotationBehavior.User;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            ConfigureXR();
            AssetDatabase.SaveAssets();
            Debug.Log("Reality Defender configured: Android API 26-36, IL2CPP ARM64, GLES3, AR Optional, manual XR startup, single non-Development APK with local debug signing.");
        }

        static void ConfigureXR()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget targets))
            {
                targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                targets.name = "RealityDefender XR Settings";
                AssetDatabase.CreateAsset(targets, SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            if (!targets.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                targets.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!targets.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                targets.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            XRGeneralSettings general = targets.SettingsForBuildTarget(BuildTargetGroup.Android);
            XRManagerSettings manager = general.Manager;
            if (!manager.activeLoaders.Any(loader => loader is ARCoreLoader) &&
                !XRPackageMetadataStore.AssignLoader(manager, typeof(ARCoreLoader).FullName, BuildTargetGroup.Android))
                throw new BuildFailedException("Could not assign the installed Google ARCore loader for Android.");
            general.InitManagerOnStart = false;
            manager.automaticLoading = false;
            manager.automaticRunning = false;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(targets);

            ARCoreSettings core = ARCoreSettings.currentSettings;
            if (!core)
            {
                core = ARCoreSettings.GetOrCreateSettings();
                core.name = "ARCore Optional Settings";
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(core)))
                    AssetDatabase.CreateAsset(core, SettingsFolder + "/ARCoreSettings.asset");
                ARCoreSettings.currentSettings = core;
            }
            core.requirement = ARCoreSettings.Requirement.Optional;
            core.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(core);
        }

        [MenuItem("Reality Defender/Android/Validate Android Project")]
        public static void ValidateProject()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (!scene) throw new BuildFailedException("Missing intended startup scene: " + ScenePath);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to the Android build target before building Reality Defender.");
            XRGeneralSettings xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!xr || !xr.Manager || !xr.Manager.activeLoaders.Any(loader => loader is ARCoreLoader))
                throw new BuildFailedException("Android Google ARCore loader is not assigned.");
            if (xr.InitManagerOnStart || xr.Manager.automaticLoading || xr.Manager.automaticRunning)
                throw new BuildFailedException("Reality Defender must initialize XR manually from the explicit AR entry flow.");
            if (!ARCoreSettings.currentSettings || ARCoreSettings.currentSettings.requirement != ARCoreSettings.Requirement.Optional ||
                ARCoreSettings.currentSettings.depth != ARCoreSettings.Requirement.Optional)
                throw new BuildFailedException("ARCore and depth must both be Optional.");
            var failures = new HashSet<BuildValidationRule>();
            MethodInfo getIssues = typeof(BuildValidator).GetMethod("GetCurrentValidationIssues", BindingFlags.NonPublic | BindingFlags.Static);
            if (getIssues == null) throw new BuildFailedException("Cannot inspect installed XR Project Validation API.");
            getIssues.Invoke(null, new object[] { failures, BuildTargetGroup.Android });
            Directory.CreateDirectory("Submission");
            File.WriteAllLines("Submission/XRValidation.txt", failures.Count == 0
                ? new[] { "No current Android XR Project Validation issues." }
                : failures.Select(f => (f.Error ? "ERROR " : "WARNING ") + f.GetDisplayString()));
            foreach (BuildValidationRule failure in failures)
            {
                if (failure.Error) Debug.LogError(failure.GetDisplayString());
                else Debug.LogWarning(failure.GetDisplayString());
            }
            if (failures.Any(f => f.Error)) throw new BuildFailedException("Resolve blocking XR Project Validation errors. See Submission/XRValidation.txt.");
            Debug.Log("Reality Defender Android settings and XR Project Validation passed.");
        }

        [MenuItem("Reality Defender/Android/Build APK")]
        public static void BuildApk()
        {
            Directory.CreateDirectory("Submission");
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var evidence = new ReportEvidence { unityVersion = Application.unityVersion, startedUtc = DateTime.UtcNow.ToString("o") };
            try
            {
                Configure();
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    throw new BuildFailedException("Launch batch Unity with -buildTarget Android (or switch target in Build Profiles).");
                EditorSceneManager.OpenScene(ScenePath);
                var mobileOrigin = UnityEngine.Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
                if (mobileOrigin) { mobileOrigin.CameraYOffset = 0f; EditorSceneManager.SaveScene(mobileOrigin.gameObject.scene); }
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                ValidateProject();
                AssetDatabase.SaveAssets();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                evidence.result = report.summary.result.ToString();
                evidence.totalErrors = report.summary.totalErrors;
                evidence.totalWarnings = report.summary.totalWarnings;
                evidence.totalBytes = report.summary.totalSize.ToString();
                evidence.durationSeconds = report.summary.totalTime.TotalSeconds;
                evidence.outputPath = report.summary.outputPath;
                WriteEvidence(evidence);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Unity Android build did not succeed: " + report.summary.result);
                var apk = new FileInfo(ApkPath);
                if (!apk.Exists || apk.Length == 0)
                    throw new BuildFailedException("BuildReport succeeded but the APK is missing or empty.");
                Debug.Log("REALITY_DEFENDER_APK_SUCCESS " + apk.FullName + " bytes=" + apk.Length);
            }
            catch (Exception exception)
            {
                evidence.result = "Failed";
                evidence.failure = exception.ToString();
                WriteEvidence(evidence);
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void WriteEvidence(ReportEvidence evidence)
        {
            evidence.finishedUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText("Submission/BuildReport.json", JsonUtility.ToJson(evidence, true));
        }

        [Serializable]
        class ReportEvidence
        {
            public string unityVersion, startedUtc, finishedUtc, result = "NotRun", outputPath = ApkPath;
            public string packageId = "com.assignment.realitydefender", architecture = "arm64-v8a", graphicsApi = "OpenGLES3";
            public string scriptingBackend = "IL2CPP", signer = "Unity local debug signer (signature inspection required)", totalBytes, failure;
            public int minSdk = 26, targetSdk = 36, totalErrors, totalWarnings;
            public bool developmentBuild = false, arOptional = true;
            public double durationSeconds;
        }
    }

    /// <summary>Prevents camera permission's implicit feature requirements from filtering Practice Mode devices.</summary>
#pragma warning disable CS0618
    public sealed class OptionalCameraManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(manifestPath);
            XmlElement manifest = document.DocumentElement;
            if (manifest == null) throw new BuildFailedException("Generated Android manifest has no root.");
            foreach (string feature in new[] { "android.hardware.camera", "android.hardware.camera.any", "android.hardware.camera.autofocus", "android.hardware.camera.ar", "com.google.ar.core.depth" })
                SetElement(document, manifest, "uses-feature", feature, "required", "false");
            XmlElement application = manifest.SelectSingleNode("application") as XmlElement;
            if (application == null) throw new BuildFailedException("Generated Android manifest has no application.");
            SetElement(document, application, "meta-data", "com.google.ar.core", "value", "optional");
            SetElement(document, application, "meta-data", "unityplayer.SkipPermissionsDialog", "value", "true");
            document.Save(manifestPath);
            Debug.Log("Reality Defender manifest: AR, camera, autofocus and depth are optional; camera permission is requested only by AR startup.");
        }

        static void SetElement(XmlDocument document, XmlElement parent, string tag, string name, string attribute, string value)
        {
            XmlElement element = parent.ChildNodes.Cast<XmlNode>().OfType<XmlElement>()
                .FirstOrDefault(child => child.Name == tag && child.GetAttribute("name", AndroidNamespace) == name);
            if (element == null)
            {
                element = document.CreateElement(tag);
                element.SetAttribute("name", AndroidNamespace, name);
                parent.AppendChild(element);
            }
            element.SetAttribute(attribute, AndroidNamespace, value);
        }
    }
#pragma warning restore CS0618
}
