using UnityEngine;

namespace PostBoxGame
{
    public class PostBoxAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip flap,paper,parcel,parcelSlide,parcelSettle,snap,good,bad,door;
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=.42f;source.spatialBlend=0;
            flap=Tone("slot flap",155,.10f,.7f);paper=Tone("paper landing",390,.08f,.22f);
            parcelSlide=Noise("parcel sliding through slot",.23f,false);
            parcel=Noise("parcel entering body",.24f,true);
            parcelSettle=Noise("parcel settling in grid",.16f,true);
            snap=Tone("grid snap",530,.055f,.25f);
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
        AudioClip Noise(string name,float seconds,bool impact)
        {
            const int rate=22050;int len=Mathf.RoundToInt(rate*seconds);var data=new float[len];
            uint state=271828183;float low=0;
            for(int i=0;i<len;i++)
            {
                state=state*1664525u+1013904223u;
                float raw=(state/(float)uint.MaxValue)*2-1;
                low+=(raw-low)*(impact?.10f:.28f);
                float u=i/(float)len;
                float envelope=impact?Mathf.Pow(1-u,3):Mathf.Sin(u*Mathf.PI)*.65f;
                float rumble=impact?Mathf.Sin(i*2*Mathf.PI*78/rate)*.30f:0;
                data[i]=(low*(impact?.75f:.45f)+raw*(impact?.12f:.28f)+rumble)*envelope*.72f;
            }
            var clip=AudioClip.Create(name,len,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Arrival(bool heavy)
        {
            source.PlayOneShot(flap);
            if(heavy){source.PlayOneShot(parcelSlide,.65f);source.PlayOneShot(parcel,.95f);}
            else source.PlayOneShot(paper);
        }
        public void Settle(bool heavy)
        {
            source.PlayOneShot(snap);
            if(heavy)source.PlayOneShot(parcelSettle,.8f);
        }
        public void Route(bool correct){source.PlayOneShot(correct?good:bad);}
        public void Collection(){source.PlayOneShot(door);}
    }
}
