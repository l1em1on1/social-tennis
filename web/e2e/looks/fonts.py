import re,sys,base64,urllib.request,glob,os
d=sys.argv[1]
want=["Barlow_Condensed_wght_600_700","Barlow_wght_400_500_600_700","Lilita_One","Nunito_wght_400__900","Baloo_2_wght_500__800","Titan_One","Outfit_wght_400__700","Figtree_wght_400__800","Big_Shoulders_Display_wght_600__900","Rubik_wght_400__800"]
out=[];cache={}
for n in want:
    t=open(os.path.join(d,n+".css")).read()
    for m in re.finditer(r"/\* latin \*/\s*(@font-face \{.*?\})",t,re.S):
        b=m.group(1); u=re.search(r"url\((https://fonts\.gstatic\.com/[^)]+)\)",b).group(1)
        if u not in cache: cache[u]=base64.b64encode(urllib.request.urlopen(u,timeout=30).read()).decode()
        out.append(b.replace(u,"data:font/woff2;base64,"+cache[u]))
open(sys.argv[2],"w").write("\n".join(out))
print(len(out),"faces",sum(map(len,out))//1024,"KB")
