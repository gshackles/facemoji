#r "System.Drawing"

open System
open System.IO
open System.Net
open System.Net.Http.Headers
open System.Drawing
open System.Drawing.Imaging
open FSharp.Data
open Newtonsoft.Json

type FaceRectangle = { Height: int; Width: int; Top: int; Left: int; }
type Scores = { Anger: float; Contempt: float; Disgust: float; Fear: float;
                Happiness: float; Neutral: float; Sadness: float; Surprise: float; }
type Face = { FaceRectangle: FaceRectangle; Scores: Scores }

let apiKey = Environment.GetEnvironmentVariable("EmotionApiKey")
let appPath = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "site", "wwwroot", "facemoji")

let getImageUrl (req: HttpRequestMessage) =
    req.GetQueryNameValuePairs()
    |> Seq.find(fun pair -> pair.Key.ToLowerInvariant() = "url")
    |> fun pair -> pair.Value

let getImage url = 
    Http.Request(url, httpMethod = "GET")
    |> fun (imageResponse) -> 
        match imageResponse.Body with
        | Binary bytes -> bytes
        | _ -> failwith "expected binary response but received text"

let getFaces bytes =
    Http.RequestString("https://api.projectoxford.ai/emotion/v1.0/recognize",
        httpMethod = "POST",
        headers = [ "Ocp-Apim-Subscription-Key", apiKey ],
        body = BinaryUpload bytes)
    |> fun (json) -> JsonConvert.DeserializeObject<Face[]>(json)

let getEmoji face =
    match face.Scores with
        | scores when scores.Anger > 0.1 -> "angry.png"
        | scores when scores.Fear > 0.1 -> "afraid.png"
        | scores when scores.Sadness > 0.1 -> "sad.png"
        | scores when scores.Happiness > 0.5 -> "happy.png"
        | _ -> "neutral.png"
    |> fun filename -> Path.Combine(appPath, filename)
    |> Image.FromFile

let drawImage (bytes: byte[]) faces =
    use inputStream = new MemoryStream(bytes)
    use image = Image.FromStream(inputStream)
    use graphics = Graphics.FromImage(image)
    
    faces |> Array.iter(fun face ->
        let rect = face.FaceRectangle
        let emoji = getEmoji face
        graphics.DrawImage(emoji, rect.Left, rect.Top, rect.Width, rect.Height)
    )

    use outputStream = new MemoryStream();
    image.Save(outputStream, ImageFormat.Jpeg)
    outputStream.ToArray()

let createResponse bytes =
    let response = new HttpResponseMessage()
    response.Content <- new ByteArrayContent(bytes)
    response.StatusCode <- HttpStatusCode.OK
    response.Content.Headers.ContentType <- MediaTypeHeaderValue("image/jpeg")
    
    response

let Run (req: HttpRequestMessage) =  
    let bytes = getImage <| getImageUrl req
    
    getFaces bytes
    |> drawImage bytes
    |> createResponse