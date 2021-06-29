using System;


namespace ImageDatabase.Helper.Tree
{
    [Serializable]
    public class CEDDTreeNode : BKTreeNode
    {
      
       
        public int GroupID { get; set; }
        public ushort[] JCTDescriptor { get; set; }

        public int Sum;

        protected override Int32 calculateDistance(BKTreeNode node)
        {

            float dist = CEDD_Descriptor.CEDD.Compare2(this.JCTDescriptor, ((CEDDTreeNode)node).JCTDescriptor, Sum, ((CEDDTreeNode)node).Sum );            
            return Convert.ToInt32(dist);
        }
    }


    [Serializable]
    public class phashTreeNode : BKTreeNode
    {

        public int GroupID { get; set; }

        public ulong phash { get; set; }
        protected override Int32 calculateDistance(BKTreeNode node)
        {

            ulong phash1 = this.phash;
            ulong phash2 = ((phashTreeNode)node).phash;
            return Shipwreck.Phash.ImagePhash.GetHammingDistance(phash1, phash2);
            // return Convert.ToInt32(dist);
        }
    }
}
