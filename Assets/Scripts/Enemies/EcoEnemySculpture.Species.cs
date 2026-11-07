using UnityEngine;

internal sealed partial class EcoEnemySculpture
{
    private void Slime()
    {
        var shadow = Paint(0x316748);
        var jelly = Paint(0x549450);
        var mantle = Paint(0x6FAD50);
        var tips = Paint(0x9FC065);
        var face = Paint(0xB2C977);
        var eyes = Paint(0x222C23);
        var pearl = Paint(0xF0E7BD);
        Ball(shadow, P(0, .38f, -.035f), P(.78f, .70f, .65f));
        Ball(jelly, P(0, .45f, .005f), P(.78f, .76f, .67f));
        Ball(face, P(0, .39f, .17f), P(.63f, .61f, .39f));
        // A continuous, scalloped mantle preserves the Salya sketch's dripping fringe.
        Open(Joint.Crest, P(0, .68f, 0));
        var cap = SlimeMantle();
        Put(cap, mantle, Vector3.zero, Vector3.one, Joint.Crest);
        Release(cap);
        Lick(tips, Joint.Crest, .115f, P(-.03f, .79f, -.04f), P(.06f, .89f, -.055f), P(.15f, .955f, -.035f), P(.25f, .94f, -.015f));
        Ball(tips, P(-.22f, .73f, .22f), P(.17f, .08f, .067f), Joint.Crest, P(-12, -15, -25));
        Ball(jelly, P(.30f, .48f, .17f), P(.155f, .22f, .105f), Joint.Crest, P(-8, -9, 23));
        EyePair(P(0, .505f, .365f), .115f, P(.081f, .165f, .043f), eyes, pearl, -4);
        Ball(Paint(0x879B56), P(0, .34f, .371f), P(.065f, .021f, .014f));
        foreach (int side in new[] { -1, 1 })
        {
            var arm = side < 0 ? Joint.LeftArm : Joint.RightArm;
            Open(arm, P(side * .328f, .395f, .055f));
            Ball(jelly, P(side * .40f, .344f, .096f), P(.255f, .17f, .21f), arm, P(-12, 0, side * -33));
            Ball(tips, P(side * .436f, .344f, .14f), P(.113f, .090f, .090f), arm);
            var foot = side < 0 ? Joint.LeftFoot : Joint.RightFoot;
            Open(foot, P(side * .19f, .08f, .15f), foot);
            Ball(shadow, P(side * .19f, .064f, .13f), P(.26f, .126f, .29f), foot);
            Ball(jelly, P(side * .19f, .08f, .207f), P(.21f, .117f, .194f), foot);
        }
    }

    private static Mesh SlimeMantle()
    {
        const int sides = 35;
        float[] radii = { 0f, .39f, .436f, .438f, .40f, .308f, .177f, 0f };
        float[] heights = { .575f, .567f, .586f, .644f, .706f, .778f, .822f, .839f };
        var vertices = new Vector3[sides * radii.Length];
        for (int row = 0; row < radii.Length; row++)
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides;
                float wave = (Mathf.Cos(6f * angle + .3f) * .044f + Mathf.Sin(3f * angle) * .034f) * Mathf.InverseLerp(7f, 1f, row);
                float rim = radii[row] * (1f + .085f * Mathf.Cos(angle * 5f - .5f));
                vertices[row * sides + i] = P(Mathf.Cos(angle) * rim, heights[row] + wave, Mathf.Sin(angle) * rim * .83f);
            }
        return Surface("Salya scalloped mantle", vertices, sides);
    }

    private void Flame()
    {
        var basalt = Paint(0x392E32);
        var cinder = Paint(0x724038);
        var crust = Paint(0xA95235);
        var deep = Paint(0xD45325);
        var flame = Paint(0xF27C26, lit: true);
        var light = Paint(0xFFC04D, lit: true);
        var core = Paint(0xFFE68D, lit: true);
        Puff(basalt, P(0, .33f, 0), P(.61f, .57f, .50f));
        Puff(cinder, P(-.055f, .40f, .105f), P(.55f, .53f, .41f), rotation: P(0, -14, 9));
        Puff(crust, P(.119f, .265f, .15f), P(.23f, .225f, .22f), rotation: P(6, 8, -20));
        EyePair(P(0, .417f, .295f), .102f, P(.069f, .116f, .045f), core, null, 13);
        Open(Joint.Crest, P(0, .52f, .015f));
        Lick(deep, Joint.Crest, .249f, P(-.025f, .51f, -.024f), P(-.16f, .72f, -.035f), P(-.13f, .89f, -.05f), P(-.04f, .95f, -.06f));
        Lick(flame, Joint.Crest, .248f, P(.07f, .53f, -.004f), P(.14f, .74f, -.04f), P(.09f, .92f, -.08f), P(.18f, 1.075f, -.03f), P(.25f, 1.14f, .01f));
        Lick(deep, Joint.Crest, .124f, P(.18f, .53f, .002f), P(.32f, .66f, .028f), P(.30f, .82f, .047f), P(.35f, .91f, .07f));
        Lick(light, Joint.Crest, .158f, P(.004f, .52f, .13f), P(-.04f, .68f, .133f), P(.068f, .80f, .123f), P(.08f, .99f, .115f));
        Lick(core, Joint.Crest, .090f, P(.04f, .543f, .21f), P(.08f, .645f, .219f), P(.048f, .79f, .184f));
        Open(Joint.Trail, P(0, .87f, 0));
        Ball(light, P(-.24f, .908f, .01f), P(.052f, .087f, .048f), Joint.Trail, P(7, 0, -27));
        Ball(flame, P(.34f, .963f, -.06f), P(.047f, .078f, .049f), Joint.Trail, P(-4, 0, 18));
        foreach (int side in new[] { -1, 1 })
        {
            var arm = side < 0 ? Joint.LeftArm : Joint.RightArm;
            Open(arm, P(side * .235f, .37f, .01f));
            Puff(cinder, P(side * .313f, .323f, .031f), P(.19f, .22f, .18f), arm, P(0, 0, -side * 37));
            Puff(basalt, P(side * .369f, .274f, .059f), P(.157f, .145f, .161f), arm);
            var foot = side < 0 ? Joint.LeftFoot : Joint.RightFoot;
            Open(foot, P(side * .13f, .099f, .087f), foot);
            Puff(basalt, P(side * .141f, .076f, .116f), P(.226f, .15f, .289f), foot);
            Puff(crust, P(side * .13f, .126f, .132f), P(.147f, .097f, .194f), foot);
        }
    }

    private void Smoke()
    {
        var soot = Paint(0x414758);
        var middle = Paint(0x5A6074);
        var plume = Paint(0x7E8395);
        var edge = Paint(0x969AAA);
        var warm = Paint(0xC2D0DC, lit: true);
        Puff(soot, P(0, .58f, .022f), P(.72f, .59f, .58f));
        Puff(middle, P(-.254f, .571f, -.019f), P(.406f, .354f, .389f), rotation: P(0, 0, -17));
        Puff(middle, P(.261f, .61f, .005f), P(.36f, .341f, .364f), rotation: P(0, 6, 21));
        Puff(middle, P(0, .713f, -.09f), P(.64f, .408f, .48f));
        Open(Joint.Crest, P(.01f, .72f, -.04f));
        Puff(plume, P(-.159f, .759f, .02f), P(.38f, .324f, .395f), Joint.Crest);
        Puff(plume, P(.103f, .824f, -.045f), P(.358f, .333f, .375f), Joint.Crest);
        Puff(edge, P(.078f, .934f, -.07f), P(.197f, .147f, .197f), Joint.Crest, P(0, 0, 23));
        Puff(plume, P(.287f, .714f, -.01f), P(.235f, .225f, .244f), Joint.Crest);
        EyePair(P(0, .596f, .30f), .111f, P(.069f, .092f, .038f), warm, Paint(0xFBF4DF, lit: true), -14);
        Open(Joint.Trail, P(0, .43f, -.045f));
        Lick(soot, Joint.Trail, .174f, P(-.06f, .45f, -.033f), P(-.18f, .30f, -.046f), P(.04f, .227f, -.070f), P(.114f, .157f, -.084f), P(.004f, .087f, -.074f), P(-.139f, .048f, -.1f), P(-.103f, -.015f, -.10f));
        Lick(middle, Joint.Trail, .095f, P(.138f, .42f, -.11f), P(.285f, .307f, -.166f), P(.264f, .254f, -.17f), P(.169f, .22f, -.188f));
    }

    private void Acid()
    {
        var shadow = Paint(0x3E583B);
        var moss = Paint(0x637747);
        var lichen = Paint(0x8C9850);
        var pale = Paint(0xAEB96A);
        var acid = Paint(0xBDD157, lit: true);
        Puff(shadow, P(0, .614f, -.002f), P(.971f, .437f, .545f));
        Puff(moss, P(-.386f, .614f, -.01f), P(.397f, .329f, .334f));
        Puff(moss, P(.385f, .628f, .03f), P(.405f, .323f, .371f));
        Puff(moss, P(0, .705f, .012f), P(.779f, .425f, .52f));
        Puff(lichen, P(-.224f, .772f, -.002f), P(.434f, .346f, .461f));
        Puff(pale, P(.037f, .802f, -.056f), P(.425f, .395f, .414f));
        Puff(lichen, P(.238f, .756f, .057f), P(.389f, .309f, .421f));
        EyePair(P(0, .624f, .276f), .15f, P(.117f, .07f, .053f), acid, Paint(0xEBF5A0, lit: true), 31);
        Ball(shadow, P(-.148f, .658f, .301f), P(.162f, .033f, .043f), rotation: P(0, -15, -30));
        Ball(shadow, P(.148f, .658f, .301f), P(.162f, .033f, .043f), rotation: P(0, 15, 30));
        for (int i = 0; i < 3; i++)
        {
            float x = -.31f + i * .31f;
            float y = i == 1 ? .306f : .363f + i * .012f;
            var joint = Joint.LeftArm + i;
            Open(joint, P(x, y, .03f));
            Ball(acid, P(x, y - .04f, .032f), P(.075f, .096f, .069f), joint);
            Lick(acid, joint, .048f, P(x, y - .054f, .032f), P(x - .012f, y + .009f, .032f), P(x, y + .085f, .031f));
            Ball(pale, P(x - .015f, y - .018f, .061f), P(.02f, .049f, .02f), joint, P(0, 0, -8));
        }
    }

    private void Tin()
    {
        var steel = Paint(0x758881, metal: true);
        var graphite = Paint(0x344743, metal: true);
        var brass = Paint(0xB69B69, metal: true);
        var copper = Paint(0x9D6248, metal: true);
        var enamel = Paint(0x4D6C63);
        var jade = Paint(0xC9E7B5, lit: true);
        Put(metal, graphite, P(0, .39f, .005f), P(.55f, .55f, .48f));
        Put(metal, steel, P(0, .425f, .007f), P(.565f, .455f, .501f));
        for (int rib = 0; rib < 5; rib++)
            Put(ring, rib == 0 || rib == 4 ? brass : graphite, P(0, .244f + rib * .075f, .007f), P(.582f, .14f, .516f));
        Put(box, brass, P(0, .43f, .262f), P(.255f, .209f, .053f));
        Put(box, enamel, P(0, .43f, .294f), P(.214f, .169f, .026f));
        for (int vent = 0; vent < 3; vent++)
            Put(box, graphite, P(-.024f, .405f + vent * .028f, .311f), P(.102f, .013f, .014f));
        Ball(Paint(0xDA8451, lit: true), P(.064f, .434f, .314f), P(.023f, .023f, .014f));
        Put(metal, brass, P(0, .646f, .013f), P(.575f, .083f, .513f));
        Ball(steel, P(0, .725f, .008f), P(.542f, .399f, .47f));
        Put(ring, graphite, P(0, .656f, .008f), P(.55f, .12f, .48f));
        Open(Joint.Crest, P(0, .865f, .0f));
        Put(metal, graphite, P(0, .909f, -.013f), P(.085f, .064f, .08f), Joint.Crest);
        Link(brass, P(0, .915f, -.014f), P(.015f, 1.007f, -.023f), .035f, Joint.Crest);
        Put(box, copper, P(.017f, 1.01f, -.02f), P(.109f, .061f, .085f), Joint.Crest);
        Ball(jade, P(.017f, 1.038f, -.014f), P(.053f, .024f, .052f), Joint.Crest);
        EyePair(P(0, .747f, .275f), .125f, P(.066f, .066f, .028f), jade, null);
        Put(metal, graphite, P(0, .652f, .273f), P(.067f, .043f, .067f), rotation: P(90, 0, 0));
        foreach (int side in new[] { -1, 1 })
        {
            var lens = P(side * .125f, .747f, .235f);
            var axis = P(90, side * 12, 0);
            Put(metal, graphite, lens, P(.131f, .069f, .131f), rotation: axis);
            Put(ring, brass, lens + Vector3.forward * .023f, P(.121f, .16f, .121f), rotation: axis);
            Ball(copper, P(side * .227f, .67f, .148f), P(.039f, .043f, .033f));
            var arm = side < 0 ? Joint.LeftArm : Joint.RightArm;
            Vector3 shoulder = P(side * .264f, .55f, .006f);
            Vector3 elbow = P(side * .366f, .407f, .033f);
            Vector3 wrist = P(side * .401f, .293f, .110f);
            Open(arm, shoulder);
            Ball(brass, shoulder, P(.158f, .16f, .155f), arm);
            Link(steel, shoulder, elbow, .123f, arm);
            Ball(graphite, elbow, P(.117f, .116f, .119f), arm);
            Link(steel, elbow, wrist, .10f, arm);
            Put(box, brass, wrist, P(.123f, .058f, .122f), arm, P(-27, 0, side * 11f));
            foreach (int claw in new[] { -1, 1 })
            {
                Vector3 a = wrist + P(0, -.02f, claw * .046f);
                Vector3 b = wrist + P(side * .025f, -.096f, claw * .062f);
                Link(graphite, a, b, .039f, arm);
                Link(brass, b, b + P(0, -.013f, -claw * .035f), .04f, arm);
            }
            var foot = side < 0 ? Joint.LeftFoot : Joint.RightFoot;
            Open(foot, P(side * .149f, .10f, .063f), foot);
            Link(graphite, P(side * .149f, .137f, .004f), P(side * .149f, .26f, .004f), .123f, foot);
            Put(box, graphite, P(side * .149f, .049f, .079f), P(.235f, .097f, .288f), foot);
            Put(box, brass, P(side * .149f, .089f, .117f), P(.191f, .088f, .222f), foot);
            Ball(copper, P(side * .148f, .498f, .304f), P(.025f, .025f, .019f));
        }
        Open(Joint.Filter, P(0, .43f, .323f));
        Put(box, graphite, P(0, .423f, .327f), P(.234f, .181f, .09f), Joint.Filter);
        Put(box, Paint(0x77B784), P(0, .426f, .371f), P(.188f, .127f, .031f), Joint.Filter);
        for (int i = -1; i <= 1; i++)
            Put(box, jade, P(i * .04f, .426f, .391f), P(.015f, .075f, .012f), Joint.Filter);
    }
}
