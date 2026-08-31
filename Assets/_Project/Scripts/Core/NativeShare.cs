using System;
using UnityEngine;

namespace MartProg.Core
{
    /// <summary>
    /// Phase 48 — hands a file to Android's native Share Sheet
    /// (<c>Intent.ACTION_SEND</c>) so a researcher can send one device's CSV
    /// straight to email, Drive, or wherever else, with a couple of taps and
    /// no cable. NOT A PORT — Godot has no equivalent, same "new, Unity-only
    /// infrastructure" category as <see cref="ResearchLogger"/>'s own
    /// <c>NotifyAndroidMediaStore()</c> and
    /// <c>LandingPageController.QuitApplication()</c> (Phase 46) — this is
    /// the third method in the project to reach outside Unity via JNI, and
    /// the first to build and fire an <c>Intent</c> rather than just call a
    /// single static utility method.
    ///
    /// **Why this exists: the study runs on multiple physical devices, not
    /// one shared one.** Each device's app install writes its own private
    /// <c>mart_prog_log.csv</c> / <c>mart_prog_feedback.csv</c> — there is no
    /// way for the app itself to reach across devices, so getting each
    /// device's data to the researcher has to happen once per device. Export
    /// Log / Export Feedback (Phase 40) already write a same-app-folder copy,
    /// but retrieving THAT off the device still meant a file manager with
    /// special access to app-private storage, or <c>adb pull</c> — real
    /// friction for a researcher managing several devices. This class is
    /// what removes that step: a normal Android share, from inside the app.
    ///
    /// **Why a FileProvider, not a raw file path.** A <c>file://</c> URI
    /// handed to another app has thrown <c>FileUriExposedException</c> since
    /// Android 7 (API 24) — apps are not allowed to expose their private
    /// storage paths directly. The supported route is
    /// <c>androidx.core.content.FileProvider</c>: it wraps a real path in a
    /// temporary, permission-scoped <c>content://</c> URI that only the
    /// receiving app can read, only for as long as that share intent is
    /// alive. That needs a matching <c>&lt;provider&gt;</c> entry in
    /// <c>AndroidManifest.xml</c> plus a small <c>file_paths.xml</c> resource
    /// naming which folder is shareable — see
    /// <c>docs/Stage48_Researcher_Share_Off_Device.md</c> for both, and for
    /// why that manifest entry is added to a file Unity itself generates
    /// (Custom Main Manifest) rather than one written from scratch here: a
    /// hand-written manifest risks silently dropping whatever Activity
    /// class, theme, or other entries this project's exact Unity/Player
    /// Settings would otherwise produce — the same "never guess at generated
    /// project files" lesson this project already paid for five times over
    /// with <c>ProjectSettings.asset</c> (Migration Spec, "Where to pick up"),
    /// just with a far worse failure mode if guessed wrong (the app not
    /// launching at all, not a wrong preloaded-asset list).
    ///
    /// **The authority string must match the manifest exactly.**
    /// <see cref="ShareFile"/> builds it as
    /// <c>Application.identifier + ".fileprovider"</c> at runtime; the
    /// manifest declares it as <c>${applicationId}.fileprovider</c>, which
    /// Gradle resolves to the same applicationId at build time. Same value,
    /// computed on both sides from the one thing that can't drift — never
    /// hardcode the package name on either side.
    /// </summary>
    public static class NativeShare
    {
        private const string FileProviderAuthoritySuffix = ".fileprovider";

        // Standard android.content.Intent flag values — stable, public,
        // unchanged across Android versions, so safe to inline as literals
        // rather than resolve reflectively:
        //   FLAG_GRANT_READ_URI_PERMISSION = 0x00000001
        //   FLAG_ACTIVITY_NEW_TASK         = 0x10000000
        private const int FlagGrantReadUriPermission = 0x00000001;
        private const int FlagActivityNewTask = 0x10000000;

        /// <summary>
        /// Opens Android's share chooser for one file. <paramref name="mimeType"/>
        /// should be the real content type ("text/csv" for every export this
        /// project writes). <paramref name="chooserTitle"/> is the label
        /// Android shows atop the app-picker list.
        ///
        /// No-op outside Android (Editor, Standalone) — logs what it would
        /// have done instead, the same fallback shape as
        /// <c>LandingPageController.QuitApplication()</c>'s
        /// <c>UNITY_EDITOR</c> branch, so calling this during Play-mode
        /// testing never breaks anything. This also means the actual share
        /// flow — the OS chooser appearing, a real app receiving a readable
        /// file — can only be verified on a real Android device, never in
        /// the Editor. See the Stage 48 doc's on-device checklist.
        ///
        /// Fire-and-forget by design: <c>startActivity()</c> has no
        /// completion callback from here (that would need
        /// <c>startActivityForResult</c>, real complexity this project
        /// doesn't need), so this can only confirm the chooser was opened,
        /// never that the researcher actually finished sending the file.
        /// Callers should log accordingly (an "Opened" result, not
        /// "Success") — see <c>ResearcherAreaController.OnShareLogClicked()</c>.
        ///
        /// **Returns whether the chooser actually opened, with the real
        /// exception message on failure — Round 5.** The first version of
        /// this method caught every exception internally and only logged it
        /// via <c>Debug.LogError</c>, which never reaches the screen (a
        /// researcher on a real device has no Console to look at, and
        /// reading device logs would mean adb, exactly the friction this
        /// phase exists to remove). That meant a caller had no way to tell
        /// a genuine failure (e.g. <c>getUriForFile()</c> throwing because
        /// the FileProvider isn't wired up right) apart from success — the
        /// status text said "ready — choose where to send it." either way.
        /// Returning the real error lets the caller show it directly in
        /// <c>StatusText</c>, so the next failure (if any) is self-diagnosing
        /// on-device instead of needing another guess.
        /// </summary>
        public static bool ShareFile(string path, string mimeType, string chooserTitle, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "no file path was provided";
                Debug.LogWarning("NativeShare.ShareFile called with an empty path — nothing to share.");
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (AndroidJavaObject javaFile = new AndroidJavaObject("java.io.File", path))
                using (AndroidJavaClass fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider"))
                {
                    string authority = Application.identifier + FileProviderAuthoritySuffix;

                    AndroidJavaObject contentUri = fileProviderClass.CallStatic<AndroidJavaObject>(
                        "getUriForFile", context, authority, javaFile);

                    using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                    {
                        intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", contentUri);
                        intent.Call<AndroidJavaObject>("setType", mimeType);
                        intent.Call<AndroidJavaObject>("addFlags", FlagGrantReadUriPermission);

                        using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
                        using (AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                                   "createChooser", intent, chooserTitle))
                        {
                            chooser.Call<AndroidJavaObject>("addFlags", FlagActivityNewTask);
                            activity.Call("startActivity", chooser);
                        }
                    }
                }

                Debug.Log("NativeShare: opened the Share Sheet for " + path);
                return true;
            }
            catch (Exception e)
            {
                errorMessage = e.GetType().Name + ": " + e.Message;
                Debug.LogError("NativeShare: could not open the Share Sheet for " + path + ": " + errorMessage);
                return false;
            }
#else
            Debug.Log("NativeShare.ShareFile is a no-op outside Android. Would have shared: " +
                      path + " (" + mimeType + ") titled \"" + chooserTitle + "\".");
            return true;
#endif
        }
    }
}
