WpfCap
======
Reusable WPF control to display high frame rate video such as WebCam or framegrabber DirectShow output. This control uses InteropBitmap introduced in .NET framework 3.5 and managed DirectShow P/Invoke. This control does not uses DirectShow.Net library, so it's completely independent.

Usage
====
The usage of the control is similar to the usage of regular WPF Image control:
<cap:CapPlayer xmlns:cap="http://schemas.sharpsoft.net/xaml"/>

You will need to set the Device to the camera to be rendered.

Credits
=====
Originally from http://wpfcap.codeplex.com/ (http://www.codeplex.com/site/users/view/tamirk)
Many bugs squashed by http://www.geertvanhorrik.com/ (https://web.archive.org/web/20120609081227/http://blog.catenalogic.com/post/2008/12/22/Retrieving-snapshots-from-a-webcam.aspx)