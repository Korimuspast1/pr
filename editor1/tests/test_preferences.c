#include <assert.h>
#include "../preferences.h"
int main(void){
 Preferences p=sanitize_preferences(20,2,2,2000);
 assert(p.fontSize==20&&p.fontFace==2&&p.spacing==2&&p.saveDelay==2000);
 p=sanitize_preferences(-1,7,-2,5);
 assert(p.fontSize==14&&p.fontFace==0&&p.spacing==1&&p.saveDelay==700);
 p=sanitize_preferences(12,0,0,700);assert(p.fontSize==12&&p.spacing==0);
 p=sanitize_preferences(24,2,2,2000);assert(p.fontSize==24);
 return 0;
}
