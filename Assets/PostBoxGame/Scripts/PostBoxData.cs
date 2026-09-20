using System;
using UnityEngine;

namespace PostBoxGame
{
    public enum MailKind { Letter, Postcard, Magazine, Parcel, Express, Fragile, Return, Forward, Misaddressed }
    public enum Route { A, B, C, D, Return, Hold }

    [Serializable] public class Resident
    {
        public string name;
        public int number;
        public int oldNumber;
        public bool vacationHold;
        public string note;
    }

    [Serializable] public class MailSpec
    {
        public MailKind kind;
        public int width = 3;
        public int height = 1;
        public int value = 100;
        public bool canRotate = true;
    }

    [CreateAssetMenu(menuName = "PostBox Game/Configuration")]
    public class PostBoxConfig : ScriptableObject
    {
        public int columns = 8;
        public int rows = 6;
        public float runSeconds = 240;
        public float firstArrivalDelay = 5;
        public float minArrivalInterval = 3.5f;
        public float maxArrivalInterval = 7;
        public Resident[] residents = new Resident[0];
        public MailSpec[] mail = new MailSpec[0];

        public static PostBoxConfig MakeDefault()
        {
            var c = CreateInstance<PostBoxConfig>();
            c.residents = new[] {
                new Resident{name="Charles Pratt",number=12,vacationHold=true,note="Vacation hold: keep his mail inside."},
                new Resident{name="Noami Clark",number=16,note="Regular delivery."},
                new Resident{name="Eric Zimmerman",number=20,note="Regular delivery."},
                new Resident{name="Burgress Voshell",number=24,note="Regular delivery."},
                new Resident{name="Ramiro Corbetta",number=28,oldNumber=18,note="Moved from 18 to 28. Forward old-address mail."},
                new Resident{name="Winnie Song",number=32,note="32 Mercer: former resident Jesse Fuchs. Return his mail."}
            };
            c.mail = new[] {
                new MailSpec{kind=MailKind.Letter,width=3,height=1,value=100},
                new MailSpec{kind=MailKind.Postcard,width=2,height=1,value=80},
                new MailSpec{kind=MailKind.Magazine,width=2,height=3,value=170},
                new MailSpec{kind=MailKind.Parcel,width=3,height=3,value=250},
                new MailSpec{kind=MailKind.Express,width=3,height=1,value=240},
                new MailSpec{kind=MailKind.Fragile,width=3,height=2,value=210},
                new MailSpec{kind=MailKind.Return,width=3,height=1,value=120},
                new MailSpec{kind=MailKind.Forward,width=3,height=1,value=180},
                new MailSpec{kind=MailKind.Misaddressed,width=3,height=1,value=120}
            };
            return c;
        }
    }

    public class MailPiece
    {
        public MailSpec spec;
        public string addressee;
        public int printedNumber;
        public Route correctRoute;
        public int x, y;
        public bool rotated;
        public float arrivedAt;
        public float deadline;
        public float settledAt;
        public int Width => rotated ? spec.height : spec.width;
        public int Height => rotated ? spec.width : spec.height;
        public string Label => addressee + "\n" + printedNumber + " Mercer Street\n" + spec.kind.ToString().ToUpperInvariant();
    }
}
