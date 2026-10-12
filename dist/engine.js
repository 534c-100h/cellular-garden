(function(root){
  const RULES = {
    life: { birth:[3], survive:[2,3], code:'B3 / S23', label:'CONWAY’S LIFE', description:'拥有 3 个邻居的空格诞生生命；拥有 2 或 3 个邻居的细胞继续存活。' },
    highlife: { birth:[3,6], survive:[2,3], code:'B36 / S23', label:'HIGHLIFE', description:'空格有 3 或 6 个邻居时诞生生命；活细胞有 2 或 3 个邻居时存活。' },
    seeds: { birth:[2], survive:[], code:'B2 / S', label:'SEEDS', description:'恰好有 2 个邻居的空格诞生生命；所有活细胞都会在下一代消逝。' }
  };
  function evolve(cells,width,height,rule,wrap=true){
    const next=new Uint8Array(cells.length);
    for(let y=0;y<height;y++) for(let x=0;x<width;x++){
      let count=0;
      for(let dy=-1;dy<=1;dy++) for(let dx=-1;dx<=1;dx++){
        if(!dx&&!dy)continue;
        let nx=x+dx,ny=y+dy;
        if(wrap){nx=(nx+width)%width;ny=(ny+height)%height;}
        else if(nx<0||ny<0||nx>=width||ny>=height)continue;
        count+=cells[ny*width+nx];
      }
      next[y*width+x]=+(cells[y*width+x]?rule.survive.includes(count):rule.birth.includes(count));
    }
    return next;
  }
  const PATTERNS={
    glider:['.O.','..O','OOO'],
    pulsar:['..OOO...OOO..','.............','O....O.O....O','O....O.O....O','O....O.O....O','..OOO...OOO..','.............','..OOO...OOO..','O....O.O....O','O....O.O....O','O....O.O....O','.............','..OOO...OOO..'],
    gun:['........................O...........','......................O.O...........','............OO......OO............OO','...........O...O....OO............OO','OO........O.....O...OO..............','OO........O...O.OO....O.O...........','..........O.....O.......O...........','...........O...O....................','............OO......................'],
    r:['.OO','OO.','.O.']
  };
  const api={RULES,PATTERNS,evolve};
  if(typeof module!=='undefined')module.exports=api;else root.Automata=api;
})(globalThis);
