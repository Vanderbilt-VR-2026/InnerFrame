using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace InnerFrame.Entrance {
public sealed class EntranceAudioProbe : MonoBehaviour {
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){ var go=new GameObject("Entrance Audio Probe"); DontDestroyOnLoad(go);go.AddComponent<EntranceAudioProbe>(); }
static float Peak(float[] samples)
{
    float peak = 0f;
    foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
    return peak;
}
IEnumerator Start(){
float[] samples = new float[1024];for(int i=0;i<12;i++){yield return new WaitForSecondsRealtime(3);AudioListener.GetOutputData(samples, 0); Debug.Log("[AudioProbe] listenerPeak="+Peak(samples)); Debug.Log("[AudioProbe] scene="+SceneManager.GetActiveScene().name+" dsp="+AudioSettings.dspTime+" listenerVolume="+AudioListener.volume+" paused="+AudioListener.pause);foreach(var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))Debug.Log("[AudioProbe] listener="+l.name+" enabled="+l.enabled);foreach(var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))Debug.Log("[AudioProbe] source="+s.name+" clip="+(s.clip?s.clip.name:"none")+" loaded="+(s.clip?s.clip.loadState.ToString():"none")+" playing="+s.isPlaying+" time="+s.time+" volume="+s.volume+" mute="+s.mute);}}
}}
