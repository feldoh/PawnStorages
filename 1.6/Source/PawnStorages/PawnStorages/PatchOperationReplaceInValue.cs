using System.Linq;
using System.Xml;
using Verse;

namespace PawnStorages;

public class PatchOperationReplaceInValue : PatchOperationPathed
{
    public string find;
    public string replace;
    public bool all = true;
    public float chance = 1.0f;

    public override bool ApplyWorker(XmlDocument xml)
    {
        bool matched = false;
        XmlNodeList xmlNodeList = xml.SelectNodes(xpath);
        if (xmlNodeList == null) return false;
        foreach (XmlNode xmlNode in xmlNodeList.Cast<XmlNode>().ToArray())
        {
            matched = true;
            if (!Rand.Chance(chance)) continue;
            xmlNode.Value = all ? xmlNode.Value.Replace(find, replace) : xmlNode.Value.ReplaceFirst(find, replace);
        }

        return matched;
    }
}
