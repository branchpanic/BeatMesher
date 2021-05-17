using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatMesher.Configuration;
using BS_Utils.Utilities;
using UnityEngine;

namespace BeatMesher
{
    public class BeatMesherController : MonoBehaviour
    {
        public static BeatMesherController Instance { get; private set; }
        public const string BeatMesherCaptureDir = "BeatMesherCaptures";

        private float recordingTime;
        private int captureFrame;

        private bool recording;
        private Saber leftSaber;
        private Saber rightSaber;

        private string levelString = "";

        public struct Capture
        {
            public float Time;
            public Vector3[] BladeBottom;
            public Vector3[] BladeTop;
        }

        private List<Capture> captures;

        public static void WriteCsv(IEnumerable<Capture> captures, string filename)
        {
            using (var sw = File.CreateText(filename))
            {
                sw.WriteLine("time,lbx,lby,lbz,rbx,rby,rbz,ltx,lty,ltz,rtx,rty,rtz");
                foreach (var capture in captures)
                {
                    sw.Write(capture.Time + ",");
                    sw.Write(capture.BladeBottom[0].AsCsv() + ",");
                    sw.Write(capture.BladeBottom[1].AsCsv() + ",");
                    sw.Write(capture.BladeTop[0].AsCsv() + ",");
                    sw.Write(capture.BladeTop[1].AsCsv() + "\n");
                }
            }
        }

        public static void WriteObj(List<Capture> captures, string filename, float spacing)
        {
            var offset = Vector3.zero;
            using (var sw = File.CreateText(filename))
            {
                for (var i = 0; i < captures.Count; i++)
                {
                    var capture = captures[i];
                    sw.WriteLine($"# t={capture.Time}");
                    sw.WriteLine((capture.BladeBottom[0] + offset).AsObjVertex());
                    sw.WriteLine((capture.BladeTop[0] + offset).AsObjVertex());
                    sw.WriteLine((capture.BladeBottom[1] + offset).AsObjVertex());
                    sw.WriteLine((capture.BladeTop[1] + offset).AsObjVertex());

                    offset += spacing * Vector3.forward;

                    if (i == 0) continue;

                    // Create faces bridging current and previous points
                    // NOTE: OBJ indices start at 1
                    sw.WriteLine($"f {4 * (i - 1) + 1} {4 * (i - 1) + 1 + 1} {4 * i + 1 + 1} {4 * i + 1}");
                    sw.WriteLine($"f {4 * (i - 1) + 2 + 1} {4 * (i - 1) + 3 + 1} {4 * i + 3 + 1} {4 * i + 2 + 1}");
                }
            }
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
                DestroyImmediate(this);
                return;
            }

            DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log?.Debug($"{name}: Awake()");

            captures = new List<Capture>();
            Directory.CreateDirectory(BeatMesherCaptureDir);
        }

        private void Start()
        {
            BSEvents.gameSceneLoaded += () =>
            {
                recording = true;

                Plugin.Log?.Info("Game scene loaded, starting to record");

                var sm = FindObjectOfType<SaberManager>();
                leftSaber = sm.leftSaber;
                rightSaber = sm.rightSaber;
            };

            // TODO: There is almost certainly a better place to find level info
            BSEvents.levelSelected += (controller, level) =>
            {
                levelString = $"{level.songName}-{level.songAuthorName}";
            };

            BSEvents.menuSceneLoaded += () =>
            {
                if (!recording) return;

                Plugin.Log?.Info("Menu scene loaded, no longer recording");
                recording = false;
                leftSaber = null;
                rightSaber = null;

                var timestamp = DateTime.Now.ToString("s").Replace(":", "-");
                var safeLevelString =
                    Path.GetInvalidFileNameChars()
                        .Aggregate(levelString, (current, ch) => current.Replace(ch, '_'));
                var filename = $"{BeatMesherCaptureDir}{Path.DirectorySeparatorChar}{timestamp}--{safeLevelString}";
                
                Plugin.Log?.Info($"Writing {captures.Count} captures to {Path.GetFullPath(filename)} as CSV and OBJ");
                WriteCsv(captures, filename + ".csv");
                WriteObj(captures, filename + ".obj", PluginConfig.Instance.ObjCaptureSpacing);

                captures.Clear();
            };
        }

        private void LateUpdate()
        {
            if (!recording) return;

            recordingTime += Time.deltaTime;
            captureFrame++;
            
            if (captureFrame < PluginConfig.Instance.FramesPerCapture) return;

            captureFrame = 0;
            captures.Add(new Capture
            {
                Time = recordingTime,
                BladeBottom = new[] {leftSaber.saberBladeBottomPos, rightSaber.saberBladeBottomPos},
                BladeTop = new[] {leftSaber.saberBladeTopPos, rightSaber.saberBladeTopPos},
            });
        }

        private void OnDestroy()
        {
            Plugin.Log?.Debug($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.
        }
    }
}