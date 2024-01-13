namespace GameNewsWasm.Records{

    public record GameRecord(int appid, string name);
    public record GameRecordDetails(int appid,string name,string? detailed_description,string header_image,string website);

   public record InfoNews(List<Newsitem> news);
 
    public class Newsitem
    {
       
        public string? title { get; set; }
        public string? author { get; set; }
        public string? contents { get; set; }
     
        public int date { get; set; }
        public string? feedname { get; set; }
    
        public int appid { get; set; }
 
    }

   
}