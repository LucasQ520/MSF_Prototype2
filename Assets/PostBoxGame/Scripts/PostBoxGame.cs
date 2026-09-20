using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PostBoxGame
{
    public class PostBoxGame : MonoBehaviour
    {
        public PostBoxConfig config;
        MarkerTracker tracker;
        PostBoxAudio audioPlayer;
        enum Phase { Calibration, Tutorial, Playing, Collection, Results }
        Phase phase;
        readonly List<MailPiece> stored=new List<MailPiece>();
        readonly Queue<MailPiece> waiting=new Queue<MailPiece>();
        MailPiece active;
        Route chosenRoute;
        float elapsed,nextArrival,tiltX,tiltY,stepCooldown,slotFlash,impact,collectionFlash;
        int received,correct,returned,wrong,expressTotal,expressGood,combo,bestCombo,score,camIndex;
        bool lParcelIssued;
        bool debug,keyboardMode,booklet,slotOpen,calibrated;
        string feedback="YOU ARE THE POST BOX";
        float feedbackTime;
        Texture2D white;
        GUIStyle heading,body,small,mailText,button,buttonDark,center,centerDark,bodyDark,smallDark,headingDark,cardTitle,resultRow,right;
        readonly Color ink=new Color(.11f,.12f,.13f),cream=new Color(.94f,.92f,.86f),gold=new Color(.91f,.70f,.31f);
        readonly Color panel=new Color(.18f,.20f,.21f),line=new Color(.36f,.39f,.40f),coral=new Color(.54f,.35f,.22f);
        readonly Color background=new Color(.055f,.06f,.065f);
        const float gx=303,gy=205,cw=79,ch=63;

        void Awake()
        {
            if(config==null)config=Resources.Load<PostBoxConfig>("PostBoxConfig");
            if(config==null)config=PostBoxConfig.MakeDefault();
            tracker=gameObject.AddComponent<MarkerTracker>();
            audioPlayer=gameObject.AddComponent<PostBoxAudio>();
            if(Camera.main==null)
            {
                var cameraObject=new GameObject("Interior View",typeof(Camera));cameraObject.transform.SetParent(transform,false);
                cameraObject.tag="MainCamera";
                var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=background;camera.cullingMask=0;
            }
            var listener=FindFirstObjectByType<AudioListener>();
            if(listener==null)(Camera.main!=null?Camera.main.gameObject:gameObject).AddComponent<AudioListener>();
            else if(!listener.enabled)listener.enabled=true;
            var canvasObject=new GameObject("PostBox Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);
            tracker.StartCamera(0);
            white=new Texture2D(1,1);white.SetPixel(0,0,Color.white);white.Apply();
            Application.targetFrameRate=60;
            phase=Phase.Calibration;
        }
        void OnDestroy(){if(white!=null)Destroy(white);}
        void StartRun()
        {
            stored.Clear();waiting.Clear();active=null;elapsed=0;nextArrival=config.firstArrivalDelay;
            received=correct=returned=wrong=expressTotal=expressGood=combo=bestCombo=score=0;
            lParcelIssued=false;
            phase=Phase.Tutorial;feedback="MOVE THE MARKER — YOUR WHOLE BODY TILTS";feedbackTime=4;
            slotOpen=keyboardMode;
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F1))debug=!debug;
            if(Input.GetKeyDown(KeyCode.F2))keyboardMode=!keyboardMode;
            if(Input.GetKeyDown(KeyCode.Tab))booklet=!booklet;
            if(phase==Phase.Calibration || phase==Phase.Results)return;
            if(phase==Phase.Collection){collectionFlash-=Time.deltaTime;if(collectionFlash<=0)phase=Phase.Results;return;}
            bool tracked=tracker.detected && calibrated && !keyboardMode;
            if(tracked)
            {
                tiltX=Mathf.Clamp((tracker.neutralCenter.x-tracker.center.x)*3.3f,-1,1);
                tiltY=Mathf.Clamp((tracker.center.y-tracker.neutralCenter.y)*4.8f,-1,1);
                float depth=tracker.area/Mathf.Max(.002f,tracker.baselineArea);
                if(slotOpen && depth<.94f)slotOpen=false;
                else if(!slotOpen && depth>1.22f)slotOpen=true;
            }
            else if(keyboardMode)
            {
                tiltX=Mathf.Lerp(tiltX,(Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),Time.deltaTime*7);
                tiltY=Mathf.Lerp(tiltY,(Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0),Time.deltaTime*7);
                slotOpen=!Input.GetKey(KeyCode.LeftShift);
            }
            else {tiltX=Mathf.Lerp(tiltX,0,Time.deltaTime*3);tiltY=Mathf.Lerp(tiltY,0,Time.deltaTime*3);}
            if(phase==Phase.Tutorial){feedbackTime-=Time.deltaTime;if(feedbackTime<=0){phase=Phase.Playing;feedback="YOU ARE THE POST BOX";feedbackTime=2;}return;}
            if(Input.GetKeyDown(KeyCode.C)){Collect();return;}
            if(!tracked && !keyboardMode){feedback="MARKER LOST — F2 FOR DEBUG CONTROL";return;}
            elapsed+=Time.deltaTime;stepCooldown-=Time.deltaTime;slotFlash-=Time.deltaTime;impact-=Time.deltaTime;collectionFlash-=Time.deltaTime;feedbackTime-=Time.deltaTime;
            if(elapsed>=config.runSeconds){Collect();return;}
            if(elapsed>=nextArrival)
            {
                if(slotOpen){var piece=Generate();waiting.Enqueue(piece);audioPlayer.Arrival(piece.spec.kind==MailKind.Parcel);received++;slotFlash=.65f;impact=.4f;
                    float t=elapsed/config.runSeconds;
                    nextArrival=elapsed+Random.Range(config.minArrivalInterval,config.maxArrivalInterval)*(1-.45f*t);
                }
            }
            if(active==null && waiting.Count>0)ActivateNext();
            if(active!=null)
            {
                if(stepCooldown<=0)
                {
                    int dx=Mathf.Abs(tiltX)>.35f?(tiltX>0?1:-1):0;
                    int dy=Mathf.Abs(tiltY)>.24f?(tiltY>0?-1:1):0;
                    if(dx!=0 || dy!=0){TryMove(dx,dy);stepCooldown=dy!=0?.14f:.18f;}
                }
                if(Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Q))Rotate();
                if(Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))AssignAndSettle();
                for(int i=0;i<6;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))chosenRoute=(Route)i;
            }
        }
        MailPiece Generate()
        {
            float t=elapsed/config.runSeconds;
            int max=t<.22f?2:t<.5f?8:config.mail.Length;
            MailSpec spec=config.mail[Random.Range(0,Mathf.Min(max,config.mail.Length))];
            if(!lParcelIssued && elapsed>=25f)
                foreach(var candidate in config.mail)
                    if(candidate.kind==MailKind.Parcel && candidate.width==3 && candidate.height==3)
                    {spec=candidate;break;}
            Resident resident=config.residents[Random.Range(0,config.residents.Length)];
            var p=new MailPiece{spec=spec,addressee=resident.name,printedNumber=resident.number,arrivedAt=elapsed,deadline=elapsed+25};
            p.lShaped=spec.kind==MailKind.Parcel && spec.width==3 && spec.height==3 && (!lParcelIssued || Random.value<.45f);
            if(p.lShaped)lParcelIssued=true;
            if(spec.kind==MailKind.Forward){resident=config.residents[4];p.addressee=resident.name;p.printedNumber=resident.oldNumber;}
            if(spec.kind==MailKind.Return){p.addressee="Jesse Fuchs";p.printedNumber=32;}
            if(spec.kind==MailKind.Misaddressed)
            {
                do p.printedNumber=Random.Range(11,40);
                while(p.printedNumber==resident.number || p.printedNumber==resident.oldNumber);
            }
            p.correctRoute=spec.kind==MailKind.Return || spec.kind==MailKind.Misaddressed?Route.Return:
                resident.vacationHold?Route.Hold:RouteFor(resident.number);
            if(spec.kind==MailKind.Express)expressTotal++;
            return p;
        }
        Route RouteFor(int number){return number<=10?Route.A:number<=20?Route.B:number<=30?Route.C:Route.D;}
        bool Fits(MailPiece p,int x,int y)
        {
            if(x<0||y<0||x+p.Width>config.columns||y+p.Height>config.rows)return false;
            foreach(var q in stored)
            {
                if(q==p)continue;
                for(int py=0;py<p.Height;py++)for(int px=0;px<p.Width;px++)
                {
                    if(!p.Occupies(px,py))continue;
                    if(q.Occupies(x+px-q.x,y+py-q.y))return false;
                }
            }
            return true;
        }
        void ActivateNext()
        {
            if(waiting.Count==0)return;
            active=waiting.Dequeue();chosenRoute=Route.A;
            for(int y=0;y<config.rows;y++)for(int x=0;x<config.columns;x++)if(Fits(active,x,y)){active.x=x;active.y=y;return;}
            feedback="YOU ARE FULL — COLLECTION CANNOT WAIT";feedbackTime=3;
            waiting.Enqueue(active);active=null;
        }
        void TryMove(int dx,int dy)
        {
            if(active==null)return;
            if(Fits(active,active.x+dx,active.y+dy)){active.x+=dx;active.y+=dy;}
            else if(Mathf.Abs(tiltY)>=Mathf.Abs(tiltX) && dy!=0 && Fits(active,active.x,active.y+dy)){active.y+=dy;}
            else if(dx!=0 && Fits(active,active.x+dx,active.y)){active.x+=dx;}
            else if(dy!=0 && Fits(active,active.x,active.y+dy)){active.y+=dy;}
        }
        void Rotate()
        {
            if(active==null||!active.spec.canRotate)return;
            active.rotated=!active.rotated;
            if(!Fits(active,active.x,active.y))active.rotated=!active.rotated;
        }
        void Settle()
        {
            if(active==null)return;
            if(!Fits(active,active.x,active.y)){feedback="NO SPACE THERE";feedbackTime=1;audioPlayer.Route(false);return;}
            bool parcel=active.spec.kind==MailKind.Parcel;
            active.settledAt=elapsed;stored.Add(active);active=null;
            audioPlayer.Settle(parcel);
            feedback="MAIL RESTING INSIDE YOU";feedbackTime=.8f;
            ActivateNext();
        }
        void Collect()
        {
            if(phase!=Phase.Playing)return;
            phase=Phase.Collection;collectionFlash=2.2f;
            audioPlayer.Collection();
            foreach(var p in stored)
            {
                // The selected route is stored as a compact value on settling via a separate lookup below.
                Route route=routes.TryGetValue(p,out var r)?r:Route.A;
                bool okay=route==p.correctRoute && (p.spec.kind!=MailKind.Express || p.settledAt<=p.deadline);
                if(okay){correct++;if(route==Route.Return)returned++;if(p.spec.kind==MailKind.Express)expressGood++;
                    combo++;bestCombo=Mathf.Max(bestCombo,combo);score+=p.spec.value+combo*10;
                }else{wrong++;combo=0;score-=p.spec.kind==MailKind.Fragile?130:75;}
            }
            wrong+=waiting.Count+(active!=null?1:0);
            score+=Mathf.RoundToInt(300f*stored.Count/Mathf.Max(1,config.columns*config.rows));
            score=Mathf.Max(0,score);
        }
        readonly Dictionary<MailPiece,Route> routes=new Dictionary<MailPiece,Route>();
        void AssignAndSettle(){if(active!=null){routes[active]=chosenRoute;Settle();}}

        void InitStyles()
        {
            if(heading!=null)return;
            heading=new GUIStyle(GUI.skin.label){fontSize=34,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};heading.normal.textColor=cream;
            body=new GUIStyle(GUI.skin.label){fontSize=20,wordWrap=true};body.normal.textColor=cream;
            small=new GUIStyle(body){fontSize=15};small.normal.textColor=new Color(.80f,.81f,.80f);
            bodyDark=new GUIStyle(body);bodyDark.normal.textColor=ink;
            smallDark=new GUIStyle(small);smallDark.normal.textColor=ink;
            headingDark=new GUIStyle(heading);headingDark.normal.textColor=ink;
            cardTitle=new GUIStyle(headingDark){fontSize=28,wordWrap=false,clipping=TextClipping.Clip};
            mailText=new GUIStyle(GUI.skin.label){fontSize=14,fontStyle=FontStyle.Bold,wordWrap=true,alignment=TextAnchor.MiddleCenter};mailText.normal.textColor=ink;
            button=new GUIStyle(GUIStyle.none){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            buttonDark=new GUIStyle(button);
            button.normal.textColor=button.hover.textColor=button.active.textColor=cream;
            buttonDark.normal.textColor=buttonDark.hover.textColor=buttonDark.active.textColor=ink;
            center=new GUIStyle(body){alignment=TextAnchor.MiddleCenter};
            centerDark=new GUIStyle(center);centerDark.normal.textColor=ink;
            resultRow=new GUIStyle(body){alignment=TextAnchor.MiddleLeft};
            right=new GUIStyle(body){alignment=TextAnchor.MiddleRight};
        }
        void Box(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,white);GUI.color=Color.white;}
        void Label(Rect r,string s,GUIStyle st){GUI.Label(r,s,st);}
        bool Button(Rect r,string s,bool selected=false,bool primary=false)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Box(new Rect(r.x,r.y+3,r.width,r.height),new Color(.02f,.025f,.03f,.65f));
            Box(r,primary?gold:selected?cream:hover?new Color(.26f,.29f,.30f):panel);
            Box(new Rect(r.x,r.y,4,r.height),primary?coral:selected?coral:line);
            return GUI.Button(r,s,primary||selected?buttonDark:button);
        }
        void OnGUI()
        {
            InitStyles();var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Box(new Rect(0,0,1280,720),background);
            if(phase==Phase.Calibration){DrawCalibration();GUI.matrix=old;return;}
            if(phase==Phase.Results){DrawResults();GUI.matrix=old;return;}
            DrawGame();GUI.matrix=old;
        }
        void DrawCalibration()
        {
            Box(new Rect(0,0,1280,103),panel);Box(new Rect(0,100,1280,3),gold);
            Label(new Rect(0,18,1280,65),"MERCER STREET  /  POST BOX NO. 04",heading);
            Box(new Rect(82,134,526,406),gold);Box(new Rect(90,142,510,390),panel);
            if(tracker.webcam!=null && tracker.webcam.width>16)GUI.DrawTexture(new Rect(100,152,490,370),tracker.webcam,ScaleMode.ScaleToFit);
            Box(new Rect(287,272,116,3),gold);Box(new Rect(287,392,116,3),gold);Box(new Rect(287,272,3,123),gold);Box(new Rect(400,272,3,123),gold);
            Label(new Rect(650,155,520,60),"CALIBRATE YOUR MARKER",heading);
            Label(new Rect(660,232,490,130),"Place a bright card in the frame. Tilt it to shift mail inside you. Bring it closer to open your slot.",body);
            Label(new Rect(660,365,490,55),tracker.message,small);
            var cams=tracker.Cameras();
            if(cams.Length>0 && Button(new Rect(660,435,220,48),"CAMERA: "+(camIndex+1)+" / "+cams.Length)){camIndex=(camIndex+1)%cams.Length;calibrated=false;tracker.StartCamera(camIndex);}
            if(Button(new Rect(660,502,220,54),"CALIBRATE",false,true))calibrated=tracker.Calibrate();
            if((calibrated||keyboardMode) && Button(new Rect(900,502,220,54),"BEGIN DAY",false,true))StartRun();
            if(Button(new Rect(660,578,460,45),keyboardMode?"DEBUG KEYBOARD: ON":"USE DEBUG KEYBOARD (F2)"))keyboardMode=!keyboardMode;
            Label(new Rect(170,590,380,45),"Blue or green card. Even light. No glare.",small);
        }
        void DrawGame()
        {
            float shake=impact>0?Mathf.Sin(Time.time*48)*impact*9:0;
            GUI.BeginGroup(new Rect(shake,0,1280,720));
            Box(new Rect(0,0,1280,90),panel);Box(new Rect(0,88,1280,3),gold);
            Box(new Rect(410,0,460,18),slotOpen?gold:new Color(.36f,.38f,.39f));
            Label(new Rect(517,19,246,30),slotOpen?"SLOT OPEN  •  MAIL CAN ENTER":"SLOT CLOSED  •  MOVE CLOSER",small);
            if(slotFlash>0){Box(new Rect(410,18,460,75),new Color(1,.85f,.49f,slotFlash*.8f));Box(new Rect(628,10,24,64),new Color(.18f,.17f,.15f));}
            Label(new Rect(25,25,240,50),"MERCER / BOX 04",body);
            Label(new Rect(1005,25,245,50),Mathf.CeilToInt(config.runSeconds-elapsed)+"s  TO COLLECTION",body);
            var bodyMatrix=GUI.matrix;
            GUI.matrix=bodyMatrix*Matrix4x4.Translate(new Vector3(tiltX*18f,-tiltY*24f,0));
            GUIUtility.RotateAroundPivot(tiltX*6f,new Vector2(640,386));
            GUIUtility.ScaleAroundPivot(new Vector2(1,1-Mathf.Abs(tiltY)*.035f),new Vector2(640,386));
            Box(new Rect(270,112,740,548),line);
            Box(new Rect(277,119,726,534),new Color(.09f,.105f,.11f));
            if(Mathf.Abs(tiltY)>.1f)
                Box(new Rect(286,tiltY<0?638:126,708,6),new Color(gold.r,gold.g,gold.b,Mathf.Abs(tiltY)*.8f));
            for(int y=0;y<config.rows;y++)for(int x=0;x<config.columns;x++)
            {
                var r=new Rect(gx+x*cw,gy+y*ch,cw-3,ch-3);
                Box(r,new Color(.24f,.27f,.28f));
            }
            foreach(var p in stored)DrawPiece(p,false);
            if(active!=null)DrawPiece(active,true);
            GUI.matrix=bodyMatrix;
            int occupied=0;foreach(var p in stored)occupied+=p.FilledCells;
            float fill=occupied/(float)(config.columns*config.rows);
            Label(new Rect(22,112,235,40),"INSIDE YOU",body);
            Box(new Rect(25,164,219,20),panel);Box(new Rect(25,164,219*fill,20),fill>.75f?coral:gold);
            Label(new Rect(25,193,235,45),fill>.8f?"YOU ARE GETTING FULL":"ROOM TO BREATHE",small);
            Label(new Rect(25,252,220,130),"TILT to slide\nR to rotate\nSPACE to settle\nTAB resident card",body);
            if(keyboardMode)Label(new Rect(25,450,230,72),"DEBUG CONTROL\nArrow keys tilt • Shift closes slot",small);
            if(phase==Phase.Playing && Button(new Rect(25,541,220,47),"COLLECT NOW  [C]",false,fill>.78f || (active==null && waiting.Count>0)))Collect();
            if(Button(new Rect(25,603,220,47),"RESIDENT CARD  [TAB]"))booklet=!booklet;
            Box(new Rect(1028,115,225,486),panel);Box(new Rect(1028,115,225,4),gold);
            Label(new Rect(1040,127,201,36),"IN YOUR SLOT",body);
            if(active!=null){Label(new Rect(1040,170,201,130),active.Label,body);Label(new Rect(1040,297,200,36),"CHOOSE ROUTE",small);}
            string[] names={"A   1–10","B   11–20","C   21–30","D   31–40","RETURN","HOLD"};
            for(int i=0;i<6;i++)
            {
                var r=new Rect(1041,335+i*38,198,34);
                if(Button(r,(i+1)+"  "+names[i],active!=null && chosenRoute==(Route)i))chosenRoute=(Route)i;
            }
            if(active!=null && Button(new Rect(1037,614,209,45),"SETTLE  [SPACE]",false,true))AssignAndSettle();
            if(booklet)DrawBooklet();
            if(phase==Phase.Collection)
            {
                Box(new Rect(270,92,740,565),new Color(1,.92f,.68f,Mathf.Clamp01(1-collectionFlash/2.2f)*.86f));
                Label(new Rect(310,315,660,90),"THEY OPEN YOU FOR COLLECTION",headingDark);
            }
            if(phase==Phase.Tutorial || feedbackTime>0){Box(new Rect(290,662,700,48),gold);Label(new Rect(300,665,680,40),feedback,centerDark);}
            else if(config.runSeconds-elapsed<30){Box(new Rect(290,662,700,48),gold);Label(new Rect(300,665,680,40),"COLLECTION IN "+Mathf.CeilToInt(config.runSeconds-elapsed)+" SECONDS",centerDark);}
            if(debug)DrawDebug(fill);
            GUI.EndGroup();
        }
        void DrawPiece(MailPiece p,bool selected)
        {
            var r=new Rect(gx+p.x*cw+2,gy+p.y*ch+2,p.Width*cw-7,p.Height*ch-7);
            Color c=p.spec.kind==MailKind.Parcel?new Color(.71f,.55f,.34f):p.spec.kind==MailKind.Magazine?new Color(.65f,.78f,.71f):new Color(.96f,.94f,.87f);
            Color stripe=selected?new Color(.78f,.54f,.15f):gold;
            if(p.lShaped)
            {
                var leg=new Rect(r.x,r.y,cw-7,3*ch-7);
                var bar=new Rect(r.x,r.y+(p.rotated?0:2*ch),3*cw-7,ch-7);
                Color parcelFace=selected?new Color(.79f,.63f,.40f):c;
                Color shadow=new Color(.025f,.025f,.025f,.55f);
                Box(new Rect(leg.x+4,leg.y+4,leg.width,leg.height),shadow);
                Box(new Rect(bar.x+4,bar.y+4,bar.width,bar.height),shadow);
                Box(leg,parcelFace);Box(bar,parcelFace);
                Box(new Rect(bar.x+13,bar.y+6,bar.width-26,5),gold);
                Label(new Rect(bar.x+13,bar.y+14,bar.width-21,bar.height-18),p.addressee+"\n"+p.printedNumber+" MERCER",mailText);
                return;
            }
            Box(r,selected?gold:c);
            Box(new Rect(r.x+5,r.y+5,r.width-10,4),stripe);
            string shortLabel=p.addressee+"\n"+p.printedNumber+" MERCER";
            if(p.Height>1)shortLabel+="\n"+p.spec.kind.ToString().ToUpperInvariant();
            Label(new Rect(r.x+5,r.y+10,r.width-10,r.height-13),shortLabel,mailText);
        }
        void DrawBooklet()
        {
            Box(new Rect(288,123,710,510),gold);
            Box(new Rect(301,137,684,482),cream);
            const string cardHeading="MERCER STREET / RESIDENT CARD";
            cardTitle.fontSize=28;
            while(cardTitle.fontSize>20 && cardTitle.CalcSize(new GUIContent(cardHeading)).x>620)cardTitle.fontSize--;
            Label(new Rect(325,153,634,39),cardHeading,cardTitle);
            Box(new Rect(334,207,620,2),gold);
            for(int i=0;i<config.residents.Length;i++)
            {
                var r=config.residents[i];
                Label(new Rect(333,220+i*61,620,58),r.name.ToUpperInvariant()+"  •  "+r.number+" MERCER\n"+r.note,bodyDark);
            }
        }
        void DrawDebug(float fill)
        {
            Box(new Rect(12,12,360,176),new Color(.02f,.03f,.04f,.92f));
            Label(new Rect(22,18,340,165),"DEBUG  F1 hide / F2 fallback\nCamera: "+tracker.cameraName+"\nMarker: "+tracker.detected+"  center: "+tracker.center.ToString("F2")+"\nArea: "+tracker.area.ToString("F3")+" / "+tracker.baselineArea.ToString("F3")+"  slot: "+(slotOpen?"OPEN":"CLOSED")+"\nTilt: "+tiltX.ToString("F2")+", "+tiltY.ToString("F2")+"  FPS: "+(1/Mathf.Max(.001f,Time.deltaTime)).ToString("F0")+"\nMail: "+received+"  fill: "+(fill*100).ToString("F0")+"%",small);
        }
        void DrawResults()
        {
            Box(new Rect(190,72,900,576),panel);Box(new Rect(190,72,900,5),gold);
            Label(new Rect(230,100,820,68),"DAY COMPLETE",heading);
            float accuracy=received>0?correct*100f/received:0;
            string[] labels={"Received","Correctly routed","Returned","Misrouted / unplaced","Accuracy","Express on time","Best combo"};
            string[] values={received.ToString(),correct.ToString(),returned.ToString(),wrong.ToString(),accuracy.ToString("F0")+"%",expressGood+" / "+expressTotal,"x"+bestCombo};
            for(int i=0;i<labels.Length;i++)
            {
                float y=205+i*41;
                if(i%2==0)Box(new Rect(290,y,700,39),new Color(.24f,.27f,.28f));
                Label(new Rect(310,y+1,470,39),labels[i],resultRow);
                Label(new Rect(810,y+1,155,39),values[i],right);
            }
            Box(new Rect(300,510,680,65),gold);Label(new Rect(310,516,660,54),"TOTAL SCORE  "+score.ToString("N0"),headingDark);
            if(Button(new Rect(490,594,300,42),"PLAY AGAIN",false,true)){routes.Clear();StartRun();}
        }
    }
}
