using UnityEngine;

namespace PostBoxGame
{
    public class PostBoxAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip flap,paper,parcel,snap,good,bad,door;
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=.25f;
            flap=Tone("slot flap",155,.10f,.7f);paper=Tone("paper landing",390,.08f,.22f);
            parcel=Tone("parcel thud",80,.20f,.8f);snap=Tone("grid snap",530,.055f,.25f);
            good=Tone("route confirmed",660,.13f,.22f);bad=Tone("invalid",115,.15f,.6f);
            door=Tone("collection door",95,.60f,.75f);
        }
        AudioClip Tone(string name,float frequency,float seconds,float roughness)
        {
            const int rate=22050;int len=Mathf.RoundToInt(rate*seconds);var data=new float[len];
            for(int i=0;i<len;i++)
            {
                float t=i/(float)rate,e=Mathf.Pow(1-i/(float)len,2);
                data[i]=(Mathf.Sin(t*frequency*6.28318f)+Mathf.Sin(t*frequency*1.97f*6.28318f)*roughness)*e*.35f;
            }
            var clip=AudioClip.Create(name,len,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Arrival(bool heavy){source.PlayOneShot(flap);source.PlayOneShot(heavy?parcel:paper);}
        public void Snap(){source.PlayOneShot(snap);}
        public void Route(bool correct){source.PlayOneShot(correct?good:bad);}
        public void Collection(){source.PlayOneShot(door);}
    }
}
