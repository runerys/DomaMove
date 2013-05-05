DomaMove
========
An application for transferring maps between instances of the Doma map archive (http://www.matstroeng.se/doma/).

Features:
- Correlates map categories by name. Make sure to have the same category set on the target server. Falls back to first category.
- Handles old versions of Doma which don't support blank maps, both as source and target.
- Handles upgraded versions of Doma which have some blank maps - as source.
- Handles both jpg and png image formats.
- Parallel processing of transfers - limited to system settings (usually 2).
- Reports anonymous usage to Google Analytics (just to see if anyone actually uses it).
- Click Once Deployment for easy installation. Though, I guess you'll only have to run it once :P
- Remembers connection settings between runs
- Only requires .NET 4 Client Profile (I had to do async Tasks without async/await, but hopefully it makes a better installer experience).

Future features: 
- Support source or target to be a local zip-archive. This means you can back up your maps.

Btw:
I won't deploy this to Windows Azure, as I would have to pay for transfer of loads of image data for users. 
And my private web provider only supports PHP.  If you want to port it to PHP, you're welcome.