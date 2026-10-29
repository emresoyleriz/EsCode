import {readFileSync,writeFileSync} from "node:fs";
import {fileURLToPath} from "node:url";
import {dirname,resolve} from "node:path";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"..");
const data=JSON.parse(readFileSync(resolve(root,"design/tokens.json"),"utf8"));
const names={background:"--bg",surface:"--surface",raised:"--raised",accent:"--accent",text:"--text",muted:"--muted",outline:"--outline",soft:"--soft"};
const colors=[];
for(const [theme,palette] of Object.entries(data.themes)){
 const declarations=[];
 for(const [name,target] of Object.entries(names)){
  const value=palette[name];
  if(typeof value!=="string"||!/^#[0-9a-f]{6}$/i.test(value))throw Error("Invalid theme token: "+theme+"."+name);
  declarations.push(target+":"+value);
 }
 const hex=palette.accent.slice(1);
 const rgb=[0,2,4].map(i=>parseInt(hex.slice(i,i+2),16));
 declarations.push("--glow:rgba("+rgb.join(",")+",.11)");
 colors.push(".theme-"+theme+"{"+declarations.join(";")+"}");
}
const result="/* GENERATED from design/tokens.json. Do not edit directly. */\n"+colors.join("\n")+"\n";
writeFileSync(resolve(root,"src/generated-tokens.css"),result,"utf8");
console.log("Theme tokens generated for "+colors.length+" themes.");
