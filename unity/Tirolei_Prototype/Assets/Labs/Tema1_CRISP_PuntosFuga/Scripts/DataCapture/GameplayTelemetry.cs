using System.IO;
using UnityEngine;
using System;

public class GameplayTelemetry : MonoBehaviour
{
    public static GameplayTelemetry Instance { get; private set; }

    private string sessionId;
    private string filePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = Guid.NewGuid().ToString().Substring(0, 8);

        filePath = Path.Combine(Application.persistentDataPath, "telemetry_gameplay.csv");

        if (!File.Exists(filePath))
        {
            string header = "sessionId,time,eventType,posX,posY,extra";
            File.WriteAllText(filePath, header + Environment.NewLine);
        }

        Debug.Log("[Telemetry] CSV path: " + filePath);
    }

    public void LogEvent(string eventType, Vector2 position, string extra = "")
    {
        try
        {
            float t = Time.time;
            string line = string.Format(
                "{0},{1:F3},{2},{3:F3},{4:F3},{5}",
                sessionId,
                t,
                eventType,
                position.x,
                position.y,
                Sanitize(extra)
            );

            File.AppendAllText(filePath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogError("[Telemetry] Error writing event: " + e.Message);
        }
    }

    private string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace(",", ";");
    }
}
