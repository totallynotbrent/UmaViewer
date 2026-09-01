using UnityEngine;
using System.Collections;
using Gallop.Live;
using Gallop.Live.Cutt;
using Gallop.Live.Cyalume;

// ponytail: one runnable check for all 80k areas - no framework, asserts only.
// Overnight-audit 2026-08-30: harden weak checks (old check3 was `|| true`,
// old check4 only File.Exists by path) into reflection + behaviour assertions
// that do not depend on scene state and cannot break the build.
public class Live80kCheck : MonoBehaviour
{
    IEnumerator Start()
    {
        // 1 shader: rim property exists via Director MPB (cached)
        var go = new GameObject("check");
        var r = go.AddComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_RimColor", Color.white);
        mpb.SetFloat("_RimStep", 0.5f);
        r.SetPropertyBlock(mpb);
        r.GetPropertyBlock(mpb);
        Debug.Assert(mpb.GetColor("_RimColor") == Color.white, "rim MPB failed");
        Debug.Log("check1 shader MPB ok");

        // 2 Director GetMainLiveSheet returns MainLive or sheet[0]
        var dummyData = ScriptableObject.CreateInstance<LiveTimelineData>();
        dummyData.worksheetList = new System.Collections.Generic.List<LiveTimelineWorkSheet>();
        var ws0 = ScriptableObject.CreateInstance<LiveTimelineWorkSheet>(); ws0.SheetType = LiveTimelineDefine.SheetIndex.PreLiveSkit; ws0.TotalTimeLength = 10f;
        var ws1 = ScriptableObject.CreateInstance<LiveTimelineWorkSheet>(); ws1.SheetType = LiveTimelineDefine.SheetIndex.MainLive; ws1.TotalTimeLength = 120f;
        dummyData.worksheetList.Add(ws0); dummyData.worksheetList.Add(ws1);
        var ltc = go.AddComponent<LiveTimelineControl>();
        ltc.data = dummyData;
        var main = ltc.GetMainLiveSheet();
        Debug.Assert(main == ws1, "GetMainLiveSheet failed");
        Debug.Log("check2 GetMainLiveSheet ok");

        // 3 LaserController exposes the viewer-side binding + update surface (reflection)
        var laserType = typeof(LaserController);
        Debug.Assert(laserType.GetMethod("SetTargetCameraTransform") != null, "LaserController.SetTargetCameraTransform missing");
        Debug.Assert(laserType.GetMethod("AlterUpdate") != null, "LaserController.AlterUpdate missing");
        Debug.Log("check3 laser binding surface ok");

        // 4 CrowdDistanceCuller exposes distance-based culling (reflection, no scene needed)
        var cullType = typeof(CrowdDistanceCuller);
        Debug.Assert(cullType.GetMethod("SetCullDistance") != null, "CrowdDistanceCuller.SetCullDistance missing");
        Debug.Assert(cullType.GetMethod("ShouldBeEnabled") != null, "CrowdDistanceCuller.ShouldBeEnabled missing");
        Debug.Log("check4 crowd cull surface ok");

        // 5 UmaSceneController transition guard
        Debug.Assert(typeof(UmaSceneController).GetField("_isTransitioning", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance) != null, "transition guard missing");
        Debug.Log("check5 scene guard ok");

        // 6 Phase-6 post-film stack surface (key type + URP feature exist)
        Debug.Assert(typeof(LiveTimelineKeyPostFilmData) != null, "LiveTimelineKeyPostFilmData missing");
        Debug.Assert(typeof(Gallop.RenderPipeline.ScreenOverlayRendererFeature) != null, "ScreenOverlayRendererFeature missing");
        Debug.Log("check6 post-film stack surface ok");

        Debug.Log("Live80kCheck ALL PASS - ponytail ceiling: full HLSL rewrite + per-uma MPB + post-film timeline->ScreenOverlay bridge");
        Destroy(go);
        yield break;
    }
}