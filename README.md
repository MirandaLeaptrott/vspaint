Summary: VSpaint adds a fully functional painting system to Vintage Story, letting you create original artwork and display it in the world.

Craft an easel, set up your canvas, and open the painting interface to draw pixel-by-pixel using a palette of paints you mix yourself. When you're happy with your work, finish the painting and hang it on the wall!

Features:

Pixel art painting interface with a full color palette
Color Palette is 'accessed' by crafted paintbrushes dipped in dye (You can hold multiple ones to access more colors at a given time)
Textures you paint display in the world.
Paintings persist across sessions and sync in multiplayer
Recipes for alternate dyes for the primary colors and shades so you can get started faster, with optional A Culinary Artillery integration for egg yolk-based binders (just like they used back in the middle-ages!)
Handbook entries to get you started




How it works:

To get started you will need to craft a Easel, a Blank Canvas, and at least one Paintbrush....you will also need some dyes to dip your brushes in.

Once you have everything, place your easel and then add your blank canvas to it. Dip your paintbrush into some dye to get started (without this, you can't paint!) and then right click to open the GUI.
You can have multiple brushes with different colors in your hotbar for ease of access.
The color selection pallet will automatically select the color on the brush in your active slot and allow you to select any other colors on other brushes on your hotbar while the GUI is open.

When you want a different color, you can dip your brush in water to wash out the current color and pick a different one.

While painting you can click "save" to update what displays on the canvas as you go for others to see. The display will also update when you close the GUI.

When you are done you can press finish and then take the painting off the easel and hang it up on a wall for all to see.

Paint Mixing: 

This mod makes use of existing vanilla dyes as the basis for its color pallet, but you can dip your brush into multiple dyes to get different colors.

This allows you to mix primary colors into secondary colors (red, blue, yellow into green, purple, and orange), and then mixing further makes brown. Blue and green makes cyan (yes, that's a stretch but....we needed something). You can dip some colors in black to get a darker version of the color (red to dark red). You can also dip some colors in white to either revert from the dark version to normal or, in the case of red, make pink.




Config:

Within the modconfigs folder you will find vspaint.json, inside you will find two new options:

{
"EnableDryingMechanics": false,
"DryAfterHours": 3.0
}


If you are having issues with the drying mechanics or just don't want them, you can turn them off. You can also tweak them instead.

Roadmap:

Pastel colors: I'll try to add either pastel or other variations on the current pallet if possible. The trick right now is keeping the colors and resolution in balance so the texture size is as optimized as possible, but I expanding the color options is on my mind.
Frames: The whole time I was working on this, I was thinking about adding in frames. Just need to make some in the modeler and work that

tessellation magic. Can't promise when these will be introduced, but soon.

Different size Canvas: right now, the one size is all we have, but I do have the intention (now that someone mentioned it on redit) to make different sizes. These will probably be smaller with the current being the largest, but I will try to make it mosaic friendly (so you can kinda choose where the painting displays on a given block-face....so you can use smaller ones to make larger overall paintings).
Different mediums: Charcoal pencils for black, wax crayons for a longer-lasting color instead of having to dip dyes (actually....this is....not that hard to make. I'll probably knock this out soon lol), and stuff like that.
Saving textures externally and making transferring those textures or even importing some easy as can be. This is pretty high priority on my list. Right now, the texture stuff is functioning by indexing the pixel data into save serialization...so a maybe a secondary export function will be needed.


Notes: This mod has been on the build for me for a while, since I found inspiration from Wanderer's Sketchbook and Collodion. Those showed me that we could have a drawing interface in the game, and that custom textures could be displayed even in a multiplayer game. From there it was digging around and breaking things until I got the code working to get the drawing stuff over to the displaying textures stuff. After many hours of work, two instances of crashing the whole block-gen system with a bloated log that recycle bin was like "nah, get that out of here", and a lot of "why isn't this facing working? every other block does this fine, why am I not getting this?" until finally it worked.....and then a surprise case of the texture data perma-binding to one spot in a world so that every future canvas becomes a copy of the previous....which was found right before I wanted to release and led to a few more hours of digging around....yay.

Where was I? Oh yeah, something about thanking those two mods and their authors for not only inspiring this and in part the source showing me how it can be done....but also for two of the best darn mods I use. The sketchbook has become my go-to for being organized in the game. I jot down notes, recipes, map out stuff....to-do lists. My son is making a little graphic novel of our adventures and my BF likes to surprise me with silly sketches of me doing something he was observing lol. And Collodion is just wonderful. I love taking photos and developing them in the game. Its so much fun to do and so immersive.




Dedication: This mod is dedicated to all your artists (including my amazing boyfriend) out there. You folks keep the world turning. What you do, what you make, what you put out there for us is important and sacred. In this time of dubious (and sometimes outright nefarious) ethics regarding the use of art gathered often without permission to be fed into engines of slop, you folks deserve all the love. Keep your heads up, keep creating, keep the humanity in our artwork.


REQUEST: I would LOVE to see y'all's art that you make in the game. That would make my day :)

Bug-reports/Feedback: Post it here and I'll get to it as soon as I can. I'm going to try and migrate stuff to github (I swear I use source control and versioning lol) to both track issues and provide source code for people who want to fork or whatever, but I've been all over the place with other projects.

I have tested this to the best of my ability, but I can't cover every possible point of failure that might crop up. I will try to address bugs as they come up and are reported. Hopefully, it'll work just fine. I tested it loading SP with no other mods and it worked fine, and I tested it on a small Lan dedicated server with two other people and that worked fine too. Also with our 200-mod-modpack and it didn't cause any additional crunch as far as I could see.


Mods I've been working on but not quite finished yet so keep an eye out for them in the future:
A Simple Sleep Solution System: Very low-impact sleep system that just looks at when you woke up and gives some debuffs if you stay up after a while. Pretty much stable and I'm adding in optional x-skills integration and such. Just a very stripped down alternative to other sleep mods.
Custom Clay Creations: Started with this a while ago, but its basically custom chiseled blocks, but they are clay so they can be fired to different colors. Made with unique tools and systems. Turns out it was easier to make the painting system than branch off the vanilla chiseling system for this lol. Not sure when I'll be done with it, but hopefully soonish. It kinda works right now, just getting them to keep the voxel data when they become fired objects...is wrinkeling my noodle.
Barbershop Update: Updating and expanded the existing Barbershop mod so that your character actually has their hair and beards growing as you play with tools to style it and dye it. Mostly this is just an update of the existing mod because its so cool.
