namespace KM.ConsoleDecode
{
    public class OutputInfo
    {
        public string 文件类型 { get; set; }
        public string 纠错类型 { get; set; }
        public int 微损度 { get; set; }
        public string 工作模态 { get; set; }
        public double 解码平均耗时_秒 { get; set; }
        public double 文件读取耗时_秒 { get; set; }
        public double 输入帧率_帧每秒 { get; set; }
        public double 输出帧率_帧每秒 { get; set; }
        public double 拆包耗时_秒 { get; set; }
        public double ECC耗时_秒 { get; set; }
        public double EDC耗时_秒 { get; set; }
        public double 分块解码耗时_秒 { get; set; }
    }
}
