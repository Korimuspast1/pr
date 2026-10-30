#ifndef EDITOR1_PREFERENCES_H
#define EDITOR1_PREFERENCES_H
typedef struct { int fontSize, fontFace, spacing, saveDelay; } Preferences;
static Preferences sanitize_preferences(int size,int face,int spacing,int delay){
 Preferences p={size>=12&&size<=24?size:14,face>=0&&face<=2?face:0,spacing>=0&&spacing<=2?spacing:1,delay==2000?2000:700};return p;
}
#endif
