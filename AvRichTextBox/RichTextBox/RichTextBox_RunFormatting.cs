using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using RtfDomParserAv;
using System.Text;
using static AvRichTextBox.FlowDocument;

namespace AvRichTextBox;

public partial class RichTextBox
{

   private static ScaleTransform strans = new(0.75, 0.75);
   internal static TransformGroup SubscriptTG = new();
   internal static TransformGroup SuperscriptTG = new();

   private void ToggleItalics()
   {
      if (IsReadOnly) return;
      FlowDoc.ToggleItalic();

   }

   private void ToggleBold()
   {
      if (IsReadOnly) return;
      FlowDoc.ToggleBold();

   }

   private void ToggleUnderlining()
   {
      if (IsReadOnly) return;
      FlowDoc.ToggleUnderlining();

   }

   private DataFormat rtbFormat = DataFormat.CreateBytesApplicationFormat("Rich-Text-Format");

   private async void CopyToClipboard()
   {
      if (DisableUserCopy) return;

      var dataObject = new DataTransfer();

      //create rtf string
      List<IEditable> newInlines = FlowDoc.GetRangeInlines(FlowDoc.Selection);
      string rtfString = RtfConversions.GetRtfFromInlines(newInlines);
      byte[] rtfbytes = Encoding.Default.GetBytes(rtfString);

      var dtiRtb = new DataTransferItem();
      dtiRtb.Set((DataFormat<byte[]>)rtbFormat, rtfbytes);
      dataObject.Add(dtiRtb);
      var dtiText = new DataTransferItem();
      dtiText.Set(DataFormat.Text, FlowDoc.Selection.GetText());
      dataObject.Add(dtiText);

      await TopLevel.GetTopLevel(this)!.Clipboard!.SetDataAsync(dataObject);

   }


   private async void PasteFromClipboard()
   {
      if (IsReadOnly) return;

      bool TextPasted = false;
      int originalSelectionStart = FlowDoc.Selection.Start;
      int newSelPoint = originalSelectionStart;

      var formats = await TopLevel.GetTopLevel(this)!.Clipboard!.GetDataFormatsAsync();
      foreach (var format in formats)
      {
         if (format == DataFormat.CreateBytesApplicationFormat("Rich Text Format"))
         {
            var rtfobj = await TopLevel.GetTopLevel(this)!.Clipboard!.TryGetDataAsync();
            if (rtfobj != null)
            {
               var bytes = await rtfobj.TryGetValueAsync((DataFormat<byte[]>)rtbFormat);
               if (bytes is not null)
               {
                  string rtfstring = System.Text.Encoding.Default.GetString(bytes!);

                  RTFDomDocument dom = new();
                  dom.LoadRTFText(rtfstring);
                  List<IEditable> insertInlines = RtfConversions.GetInlinesFromRtf(dom);
                  insertInlines.Reverse();
                  int addedchars = FlowDoc.PasteInlinesIntoRange(FlowDoc.Selection, insertInlines);

                  newSelPoint = Math.Min(newSelPoint + addedchars, FlowDoc.DocEndPoint - 1);

                  TextPasted = true;
               }

            }
            else if (format == DataFormat.Text)
            {
               var pasteText = await TopLevel.GetTopLevel(this).Clipboard.TryGetTextAsync();
               if (pasteText is not null)
               {
                  FlowDoc.SetRangeToText(FlowDoc.Selection, pasteText);
                  newSelPoint = Math.Min(newSelPoint + pasteText.Length, FlowDoc.DocEndPoint - 1);
                  TextPasted = true;
               }
            }
            else
            {
               Console.WriteLine($"Clipboard contains unsupported format: {format}");
            }
         }



         if (TextPasted)
         {
            this.DocIC.UpdateLayout();
            await Task.Delay(100); //necessary for following operations

            FlowDoc.Selection.EndParagraph.CallRequestInlinesUpdate();  // important
            FlowDoc.Selection.EndParagraph.UpdateEditableRunPositions();

            FlowDoc.Select(newSelPoint, 0);
            FlowDoc.UpdateSelection();

            FlowDoc.Selection.BiasForwardStart = false;
            FlowDoc.Selection.BiasForwardEnd = false;
            FlowDoc.SelectionExtendMode = ExtendMode.ExtendModeNone;

            CreateClient();


         }

      }


   }
}
