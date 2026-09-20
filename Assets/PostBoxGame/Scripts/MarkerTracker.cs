using UnityEngine;

namespace PostBoxGame
{
    public class MarkerTracker : MonoBehaviour
    {
        public float hueTolerance = .075f, saturationTolerance = .34f, valueTolerance = .35f;
        public float smoothing = 9f;
        public string cameraName = "";
        public bool detected;
        public Vector2 center = new Vector2(.5f,.5f);
        public Vector2 neutralCenter = new Vector2(.5f,.5f);
        public float area;
        public float baselineArea = .03f;
        public string message = "";
        public WebCamTexture webcam { get; private set; }
        Color target = Color.yellow;
        bool calibrated;
        Color32[] pixels;

        public string[] Cameras()
        {
            var d = WebCamTexture.devices; var names = new string[d.Length];
            for (int i=0;i<d.Length;i++) names[i]=d[i].name;
            return names;
        }
        public void StartCamera(int index)
        {
            if(webcam!=null) webcam.Stop();
            pixels=null;calibrated=false;detected=false;
            var d=WebCamTexture.devices;
            if(d.Length==0){message="No camera found. Use DEBUG controls."; return;}
            index=Mathf.Clamp(index,0,d.Length-1); cameraName=d[index].name;
            webcam=new WebCamTexture(cameraName,320,240,15); webcam.Play();
            message="Hold a bright card in the center box, then calibrate.";
        }
        public bool Calibrate()
        {
            if(!TryReadFrame(out int w,out int h))return false;
            Vector3 sum=Vector3.zero; int count=0;
            for(int y=h*2/5;y<h*3/5;y+=3)
            for(int x=w*2/5;x<w*3/5;x+=3){var p=pixels[y*w+x];sum+=new Vector3(p.r,p.g,p.b);count++;}
            if(count==0)return false;
            target=new Color(sum.x/count/255f,sum.y/count/255f,sum.z/count/255f);
            Color.RGBToHSV(target,out _,out float s,out _);
            if(s<.18f){message="Marker needs a stronger color.";return false;}
            Color.RGBToHSV(target,out float th,out float ts,out float tv);
            int matching=0,total=0;float sx=0,sy=0;
            for(int y=0;y<h;y+=3)for(int x=0;x<w;x+=3)
            {
                total++;var p=pixels[y*w+x];
                Color.RGBToHSV(new Color(p.r/255f,p.g/255f,p.b/255f),out float ph,out float ps,out float pv);
                float dh=Mathf.Abs(ph-th);dh=Mathf.Min(dh,1-dh);
                if(dh<hueTolerance && Mathf.Abs(ps-ts)<saturationTolerance && Mathf.Abs(pv-tv)<valueTolerance && ps>.16f){matching++;sx+=x;sy+=y;}
            }
            baselineArea=matching/(float)Mathf.Max(1,total);
            if(baselineArea<.002f){message="Could not find enough of the card. Fill the target box.";return false;}
            neutralCenter=new Vector2(sx/matching/w,sy/matching/h);
            center=neutralCenter;area=baselineArea;detected=true;calibrated=true;
            message="Calibrated. Move closer to OPEN; move back to CLOSE."; return true;
        }
        void Update()
        {
            if(!calibrated || webcam==null || !webcam.isPlaying || !webcam.didUpdateThisFrame)return;
            if(!TryReadFrame(out int w,out int h)){detected=false;return;}
            Color.RGBToHSV(target,out float th,out float ts,out float tv);
            int count=0;float sx=0,sy=0;
            for(int y=0;y<h;y+=3)for(int x=0;x<w;x+=3)
            {
                var p=pixels[y*w+x]; Color.RGBToHSV(new Color(p.r/255f,p.g/255f,p.b/255f),out float ph,out float ps,out float pv);
                float dh=Mathf.Abs(ph-th);dh=Mathf.Min(dh,1-dh);
                if(dh<hueTolerance && Mathf.Abs(ps-ts)<saturationTolerance && Mathf.Abs(pv-tv)<valueTolerance && ps>.16f){count++;sx+=x;sy+=y;}
            }
            float measured=count/(float)(Mathf.CeilToInt(w/3f)*Mathf.CeilToInt(h/3f));
            detected=count>15 && measured>.002f;
            if(detected)
            {
                float k=1-Mathf.Exp(-smoothing*Time.deltaTime);
                center=Vector2.Lerp(center,new Vector2(sx/count/w,sy/count/h),k);
                area=Mathf.Lerp(area,measured,k);
            }
            else message="MARKER LOST — hold it in view";
        }
        bool TryReadFrame(out int width,out int height)
        {
            width=webcam!=null?webcam.width:0;height=webcam!=null?webcam.height:0;
            if(webcam==null || !webcam.isPlaying || width<32 || height<32)
            {message="Camera is not ready.";return false;}
            try
            {
                int length=width*height;
                if(pixels==null || pixels.Length!=length)pixels=new Color32[length];
                pixels=webcam.GetPixels32(pixels);
                return pixels!=null && pixels.Length==length;
            }
            catch(System.ArgumentException)
            {
                pixels=null;
                message="Camera frame changed. Try calibrating again.";
                return false;
            }
        }
        void OnDestroy(){if(webcam!=null)webcam.Stop();}
    }
}
