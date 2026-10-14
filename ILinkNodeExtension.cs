using Lab404.Link;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KM.ConsoleDecode
{
    public static class ILinkNodeExtension
    {

        public static LinkNode ToLabLinkNode(this Decompress.LinkNode node)
        {
            return new LinkNode()
            {
                Name = node.Name,
                Level = (int)node.Level,
                Status = node.Status,
                Children = node.Children.ToLabLinkNode()
            };
        }

        public static IEnumerable<LinkNode> ToLabLinkNode(this IEnumerable<Decompress.LinkNode> oldnodes)
        {
            List<LinkNode> result = new List<LinkNode>();

            if (oldnodes == null)
            {
                return result;
            }


            foreach(var node in oldnodes)
            {
                result.Add(new LinkNode()
                {
                    Name = node.Name,
                    Level = (int)node.Level,
                    Status = node.Status,
                    Children = node.Children.ToLabLinkNode()
                });
            }
            return result;
        }
    }
}
