namespace GameNewsWasm.Records{

    public record GameRecord(int appid, string name);
    public record GameRecordDetails(string name,string detailed_description,string header_image,string website);

}