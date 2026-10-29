import {test} from "node:test";
import {strict as assert} from "node:assert";
import {readdirSync,readFileSync} from "node:fs";
import {join} from "node:path";
const dir="contracts";
const schemas=readdirSync(dir).filter(name=>name.endsWith(".schema.json"));
test("fifteen versioned contract schemas are present",()=>assert.equal(schemas.length,15));
for(const name of schemas){
test(name+" is valid structural JSON Schema",()=>{
 const s=JSON.parse(readFileSync(join(dir,name),"utf8"));
 assert.equal(s.$schema,"https://json-schema.org/draft/2020-12/schema");
 assert.equal(s.type,"object");
 assert.equal(s.additionalProperties,false);
 assert.equal(s.properties.ContractVersion.const,1);
 assert.ok(s.required.includes("ContractVersion"));
 for(const f of s.required)assert.ok(Object.hasOwn(s.properties,f),"missing property: "+f);
 for(const [field,def] of Object.entries(s.properties))assert.ok(["string","array","integer","number","boolean","object"].includes(def.type),field);
});
}