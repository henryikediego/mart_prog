using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public sealed class LociFakeShellWindow : EditorWindow
{
    private string topicId = "list";
    private string nonce = "editor-ping";
    private float textScale = 1f;
    private Vector2 scroll;

    [MenuItem("Loci/Contract Fake Shell")]
    private static void Open()
    {
        GetWindow<LociFakeShellWindow>("Loci Fake Shell");
    }

    private void OnEnable()
    {
        SpikeBridge.MessageSentToShell += OnUnityMessage;
    }

    private void OnDisable()
    {
        SpikeBridge.MessageSentToShell -= OnUnityMessage;
    }

    private void OnUnityMessage(string json)
    {
        Debug.Log("[Loci Fake Shell] Unity -> shell: " + json);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Send contract v1 commands to the LociBridge scene object.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();
        topicId = EditorGUILayout.TextField("Topic ID", topicId);
        nonce = EditorGUILayout.TextField("Ping nonce", nonce);
        textScale = EditorGUILayout.FloatField("Text scale", textScale);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (GUILayout.Button("session.start")) Send("session.start", new JObject());
        if (GUILayout.Button("session.stop")) Send("session.stop", new JObject());
        if (GUILayout.Button("placement.confirm")) Send("placement.confirm", new JObject());
        if (GUILayout.Button("placement.reset")) Send("placement.reset", new JObject());
        if (GUILayout.Button("topic.open")) Send("topic.open", new JObject { ["topicId"] = topicId });
        if (GUILayout.Button("topic.close")) Send("topic.close", new JObject());
        if (GUILayout.Button("settings.update")) Send("settings.update", new JObject { ["textScale"] = textScale });
        if (GUILayout.Button("diag.ping (expect diag.pong)")) Send("diag.ping", new JObject { ["nonce"] = nonce });
        EditorGUILayout.EndScrollView();
    }

    private static void Send(string type, JObject payload)
    {
        var bridgeObject = GameObject.Find("LociBridge");
        if (bridgeObject == null)
        {
            Debug.LogError("[Loci Fake Shell] No GameObject named LociBridge exists in the open scene.");
            return;
        }

        var bridge = bridgeObject.GetComponent<SpikeBridge>();
        if (bridge == null)
        {
            Debug.LogError("[Loci Fake Shell] The LociBridge object has no bridge component.");
            return;
        }

        var message = new JObject
        {
            ["v"] = 1,
            ["id"] = "editor-" + Guid.NewGuid().ToString("N"),
            ["type"] = type,
            ["payload"] = payload
        };
        bridge.ReceiveFromShell(message.ToString(Newtonsoft.Json.Formatting.None));
    }
}
